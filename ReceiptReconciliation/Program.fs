type Receipt =
    {
        FilePath : string
        FileDate : System.DateTime
        Charges : float array
        Dates : System.DateTime array
    }

let moneyRegex = new System.Text.RegularExpressions.Regex("[0-9]+[., ]+[0-9][0-9]")
let dateRegex = new System.Text.RegularExpressions.Regex("[0-9]+/[0-9]+/[0-9][0-9]+")

let ReadReceipt( filePath ) =
    //Extract datetime from file name
    //82817 758 PM Office Lens.txt
    let fileDateString = System.IO.Path.GetFileNameWithoutExtension( filePath ).Replace("Office Lens","").Trim()
    let split = fileDateString.Split(' ')
    if split.Length <> 3 then failwith "receipt name format invalid"
    let dateString,timeString,amPm = split.[0],split.[1],split.[2]
    let minute = timeString.Substring(timeString.Length - 2) //always has two decimal places
    let hour = timeString.Substring(0,timeString.Length - 2)
    let year = dateString.Substring(dateString.Length - 2) //always has two decimal places
    //NOTE their is ambiguity here: 12117 could be Jan 21, 17 or Dec 1, 17
    //we assume the most recent month
    let currentMonth = System.DateTime.Now.Month
    let year = dateString.Substring(dateString.Length - 2)

    let month = 
        if currentMonth = 12 then 
            [12 .. -1 .. 1] |> List.find( fun m -> dateString.StartsWith(m.ToString()) )
        else
            [currentMonth .. -1 .. 1] @ [12 .. -1 .. currentMonth] |> List.find( fun m -> dateString.StartsWith(m.ToString()) )
    let day = dateString.Remove(dateString.Length - 2).Remove( 0, month.ToString().Length ) //remove year and month, day remains
    let dateTime = new System.DateTime( System.Int32.Parse("20" + year), month, System.Int32.Parse(day), System.Int32.Parse(hour) + (if amPm = "AM" or hour = "12" then 0 else 12), System.Int32.Parse(minute), 0 )
    
    //use regex to search for dates and charges in file
    let lines = System.IO.File.ReadAllLines( filePath )
    let charges = lines |> Seq.map( fun l -> moneyRegex.Match(l) ) |> Seq.filter( fun m -> m.Success) |> Seq.choose( fun m -> match m.Value.Replace(",",".").Replace(" ","." ).Replace("..","." ) |> System.Double.TryParse with | (true,int) -> Some(int) | (false,_) -> None ) |> Seq.toArray
    let dates = lines |> Seq.map( fun l -> dateRegex.Match(l) ) |> Seq.filter( fun m -> m.Success) |> Seq.map( fun m -> m.Value |> System.DateTime.Parse ) |> Seq.toArray
       
    {FilePath=filePath; FileDate=dateTime; Charges=charges; Dates=dates}

let ReadTransactions( transactionsPath ) =
    let chargeIndex = 4
    let delim = ','
    transactionsPath 
    |> System.IO.File.ReadAllLines
    |> Seq.map( fun l ->
        let s = l.Split(delim)
        let charge = s.[chargeIndex] |> System.Double.Parse |> System.Math.Abs
        s,charge
        )
[<EntryPoint>]
let main argv = 
    //transactions are charges like you would find on a bank statement
    let transactionsPath = @"/z/aolney/repos/receipt-checker/September2017_0403.csv"
    let transactions = transactionsPath |> ReadTransactions 
    
    //receipts are OCR'd till receipts
    ReadReceipt @"/z/aolney/repos/receipt-checker/Office Lens/9917 1242 PM Office Lens.txt"
    printfn "%A" argv
    0 // return an integer exit code
