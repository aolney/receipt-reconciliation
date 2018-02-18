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

let ReadTransactions (delim : char ) (chargeIndex:int) ( transactionsPath : string ) =
    transactionsPath 
    |> System.IO.File.ReadAllLines
    |> Seq.skip 1 //skip header
    |> Seq.map( fun l ->
        let s = l.Split(delim)
        let charge = s.[chargeIndex] |> System.Double.Parse |> System.Math.Abs
        s,charge
        )
        
[<EntryPoint>]
let main argv = 
    let mutable args = argv
    #if DEBUG
    args <- [| @"/z/aolney/repos/receipt-checker/statement.csv"; @"/z/aolney/repos/receipt-checker/Office Lens" |]
    #endif
    if args.Length <> 2 then
        System.Console.WriteLine("Usage: ReceiptReconciliation.exe transactionsPath receiptDirectoryPath")
    else
        //transactions are charges like you would find on a bank statement
        let transactionsPath = args.[0] //@"/z/aolney/repos/receipt-checker/September2017_0403-tab.csv"
        let chargeIndex = 4
        let delimChar = '\t'
        let transactions = transactionsPath |> ReadTransactions delimChar chargeIndex
        
        //receipts are OCR'd till receipts
        let receiptsPath = args.[1] //@"/z/aolney/repos/receipt-checker/Office Lens"
        let receipts = receiptsPath |> System.IO.Directory.GetFiles |> Seq.filter( fun f -> f.EndsWith(".txt") ) |> Seq.map ReadReceipt |> Seq.toArray//OCR files are txt
        
        //create a charge to receipt index; the same charge value can appear on multiple receipts, so store in list
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
        let delimString = delimChar.ToString()
        let reconciledTransactions =
            transactions
            |> Seq.map( fun (cols,charge) -> 
                if chargeReceiptDict.ContainsKey( charge) |> not then
                    String.concat delimString cols
                else
                    let receiptSummary = chargeReceiptDict.[charge] |> Seq.map( fun r -> r.SummaryString(charge) ) //|> Seq.toArray
                    (String.concat delimString cols) + delimString + (String.concat delimString receiptSummary)
                ) //|> Seq.toArray
        //ReadReceipt @"/z/aolney/repos/receipt-checker/Office Lens/9917 1242 PM Office Lens.txt"
        
        System.IO.File.WriteAllLines( transactionsPath + "-Reconciled.csv", reconciledTransactions)
    
    0 // return an integer exit code