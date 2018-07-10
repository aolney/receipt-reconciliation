#r @"packages/FSharp.Data/lib/net45/FSharp.Data"

open FSharp.Data

open System

type Receipt =
    {
        FilePath : string
        FileDate : System.DateTime
        Charges : float array
        Dates : System.DateTime array
    } 
    member x.SummaryString(targetCharge:float) =  
        (if x.Charges.Length > 0 then 
            match x.Charges |> Seq.tryFind( fun c -> c = targetCharge) with
            | Some(charge) -> charge  //try to find the specific charge
            | None -> x.Charges |> Seq.max  //otherwise Max is probably the total (in a perfect OCR)
        else 0.0).ToString()  
            + " | " +
        (if x.Dates.Length > 0 then x.Dates.[0] else x.FileDate).ToShortDateString() //The first date is usually the transaction date
            + " | " +
        System.IO.Path.GetFileNameWithoutExtension(x.FilePath)

let moneyRegex = System.Text.RegularExpressions.Regex("[0-9]+[., ]+[0-9][0-9]")
let dateRegex = System.Text.RegularExpressions.Regex("[0-9]+/[0-9]+/[0-9][0-9]+")

let ReadReceipt( filePath ) =
    //Extract datetime from file name
    //82817 758 PM Office Lens.txt
    let fileDateString = System.IO.Path.GetFileNameWithoutExtension( filePath ).Replace("Office Lens","").Trim()
    let split = fileDateString.Split(' ')
    //if split.Length <> 3 then failwith ("receipt name format invalid: " + fileDateString) //TODO we can get fails if we image multiple receipts in the same minute;  currently we don't care about the ambiguity because we're not requiring dates to be unique, but this could be a future problem
    let dateString,timeString,amPm = split.[0],split.[1],split.[2]
    let minute = timeString.Substring(timeString.Length - 2) //always has two decimal places
    let hour = timeString.Substring(0,timeString.Length - 2)
    let year = dateString.Substring(dateString.Length - 2) //always has two decimal places
   
    //month has unambiguous and ambiguous cases
    let month = 
        //unambiguous case, e.g. 112317 must be 11-23-17
        if dateString.Length = 6 then 
            System.Int32.Parse(  dateString.Substring(0,2) )
        //ambiguous case, e.g. 11217 is either 11-2-17 or 1-12-17
        else
            //use most recent month to disambiguate; 
            //count backward from current month - this is a heuristic and may fail
            let currentMonth = System.DateTime.Now.Month
            if currentMonth = 12 then 
                [12 .. -1 .. 1] |> List.find( fun m -> dateString.StartsWith(m.ToString()) )
            else
                [currentMonth .. -1 .. 1] @ [12 .. -1 .. currentMonth] |> List.find( fun m -> dateString.StartsWith(m.ToString()) )

    let day = dateString.Remove(dateString.Length - 2).Remove( 0, month.ToString().Length ) //remove year and month, day remains
    let dateTime = 
        try
            System.DateTime( System.Int32.Parse("20" + year), month, System.Int32.Parse(day), System.Int32.Parse(hour) + (if amPm = "AM" || hour = "12" then 0 else 12), System.Int32.Parse(minute), 0 )
        with
        | e -> 
            //when we fail here it is typically because of ambiguities from Len's naming convention
            printfn "%s" (e.Message + "\n" + filePath + "\n" + "year:" + year.ToString() + " month:" + month.ToString() + " day:" + day.ToString() + " hour:" + hour.ToString())
            DateTime.MinValue
    
    //use regex to search for dates and charges in file
    let lines = System.IO.File.ReadAllLines( filePath )
    let charges = lines |> Seq.map( fun l -> moneyRegex.Match(l) ) |> Seq.filter( fun m -> m.Success) |> Seq.choose( fun m -> match m.Value.Replace(",",".").Replace(" ","." ).Replace("..","." ) |> System.Double.TryParse with | (true,int) -> Some(int) | (false,_) -> None ) |> Seq.toArray
    let dates = lines |> Seq.map( fun l -> dateRegex.Match(l) ) |> Seq.filter( fun m -> m.Success) |> Seq.map( fun m -> m.Value |> System.DateTime.Parse ) |> Seq.toArray
       
    {FilePath=filePath; FileDate=dateTime; Charges=charges; Dates=dates}

//Returns a tuple: the tokenized row of the statement * value of the charge
let ReadStatements (delim : string ) (chargeIndex:int) ( statementsDirectoryPath : string ) =
    statementsDirectoryPath
    |> System.IO.Directory.GetFiles 
    |> Array.filter( fun f -> f.EndsWith(".csv") )
    |> Array.collect( 
        fun filePath -> 
            let file = CsvFile.Load(filePath,delim) //TODO something wrong with quotes
            file.Rows
            |> Array.ofSeq
            |> Array.map( fun row ->
                let charge = row.[chargeIndex] |> System.Double.Parse |> System.Math.Abs
                row,charge
                )
    )
        

if fsi.CommandLineArgs.Length <> 3 then
    System.Console.WriteLine("Usage: fsharpi ReceiptReconciliation.fsx statementsDirectoryPath receiptDirectoryPath")
else
    //put your custom values here
    let chargeIndex = 4     //column your charges appear in on your statement below
    let delim = "," //defaults to comma


    //csv format bank statement
    let statementsDirectoryPath = fsi.CommandLineArgs.[1] //@"/z/aolney/repos/receipt-checker/September2017_0403-tab.csv"
    let statements = statementsDirectoryPath |> ReadStatements delim chargeIndex
    
    //receipts are OCR'd till receipts
    let receiptsPath = fsi.CommandLineArgs.[2] //@"/z/aolney/repos/receipt-checker/Office Lens"
    let receipts = receiptsPath |> System.IO.Directory.GetFiles |> Array.filter( fun f -> f.EndsWith(".txt") ) |> Array.map ReadReceipt 
    
    //Using the receipts only, create a charge to receipt index; the same charge value can appear on multiple receipts, so store in list
    let chargeReceiptDict = new System.Collections.Generic.Dictionary<float,System.Collections.Generic.List<Receipt>>()
    for r in receipts do
        for c in r.Charges do
            if chargeReceiptDict.ContainsKey( c ) |> not then 
                chargeReceiptDict.Add(c,new System.Collections.Generic.List<Receipt>() )
            chargeReceiptDict.[c].Add( r )
        //because I tip to round numbers, add a pseudo receipt by adding 15% and rounding to nearest dollar
        //this is really sketchy; probably should only apply to highest amount or false alarms too much
//            for c in r.Charges |> Array.map( fun charge -> System.Math.Ceiling(charge + charge * 0.15) ) do
//                if chargeReceiptDict.ContainsKey( c ) |> not then 
//                    chargeReceiptDict.Add(c,new System.Collections.Generic.List<Receipt>() )
//                chargeReceiptDict.[c].Add( r )
        
    //traverse transactions, finding all matching receipts
    let delimString = delim.ToString()
    let reconciledStatements =
        statements
        |> Array.map( fun (csvRow,charge) -> 

            //Some elements contain delimiters, so we need to enclose the columns before writing to file
            let quotedRow = csvRow.Columns |> Array.map( fun x -> "\"" + x + "\"")

            //If we can't match the charge just print the original row
            if chargeReceiptDict.ContainsKey( charge) |> not then
                String.concat delimString quotedRow
            //If we can match the charge, pring the original row plus the matching receipts
            else
                let receiptSummary = chargeReceiptDict.[charge] |> Seq.map( fun r -> r.SummaryString(charge) ) //|> Seq.toArray
                (String.concat delimString quotedRow) + delimString + (String.concat delimString receiptSummary)
            ) 

    System.Console.WriteLine("Writing " +  statementsDirectoryPath + "-Reconciled.csv")
    System.IO.File.WriteAllLines( statementsDirectoryPath + "-Reconciled.csv", reconciledStatements)
