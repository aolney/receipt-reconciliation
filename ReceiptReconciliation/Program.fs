type Receipt =
    {
        FileName : string
        FileDate : System.DateTime
        Charges : float array option
        Dates : float array option
    }

let moneyRegex = new System.Text.RegularExpressions.Regex("[0-9]+[., ][0-9][0-9]")
let dateRegex = new System.Text.RegularExpressions.Regex("[0-9][0-9]/[0-9][0-9]/[0-9][0-9]")

let ReadReceipt( filePath ) =
    //82817 758 PM Office Lens.txt
    
    let fileDate = new System.DateTime()
[<EntryPoint>]
let main argv = 
    printfn "%A" argv
    0 // return an integer exit code
