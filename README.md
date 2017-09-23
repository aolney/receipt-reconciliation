# What is it?

Sometimes you want to check your receipts against your bank statements. If you find a charge without a matching receipt, it might be fraudulent.

Matching receipts can be tedious and error prone.

This simple program matches OCR receipts against a list of transactions.

# What you need for this to work

ReceiptReconciliation depends on other tools. It is simple enough you could probably swap these out for others:

- A smartphone with a camera. You will use this to scan receipts as you buy things.
- The [Office Lens app](https://www.microsoft.com/en-us/store/p/office-lens/9wzdncrfj3t8). I tried others but this was the most flexible. No API is needed to get results!
- A [OneDrive account](https://onedrive.live.com/about/en-us/). This is where Office Lens will store OCR'd receipts. Your API is downloading a folder.
- A bank that will let you download transactions as a spreadsheet
- A computer to run this program :)

# How to use it

1. As you buy things and get receipts, scan them with Office Lens and save to Word format. This will do OCR and save to OneDrive. You can even scan your computer screen for online purchases!
2. When you want to reconcile, log into OneDrive and download Documents\Office Lens, which contains all the receipts
3. Use [pandoc](https://pandoc.org/) to convert the docx Word files to txt. There is a script included so you can do this as a batch process. Run the script from the Office Lens receipt directory you downloaded.
4. Save a copy of your transactions spreadsheet as tab delimited
5. Run ReceiptReconciliation


# Some parting thoughts

- Some things are hard coded, like the column in the spreadsheet that has the charges.
- If you want good results, make sure the OCR is good by checking the Word documents after you scan.
- Receipts don't seem to have very standard formats, so getting an approximate match and then manually checking seems the best approach.


