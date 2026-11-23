param([string]$Dir)
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
# T-0753: CSV-Exporte mit Windows PowerShell (Import-Csv, Microsoft) lesen – unabhaengig von Trendo
"Werkzeug: Windows PowerShell $($PSVersionTable.PSVersion) Import-Csv"
foreach ($n in 'artikel.csv','kunden.csv','bestellungen.csv') {
  $rows = @(Import-Csv -Path (Join-Path $Dir $n) -Encoding UTF8)
  $cols = ($rows[0].PSObject.Properties | ForEach-Object Name)
  "$n : $($rows.Count) Datenzeilen, $($cols.Count) Spalten"
  foreach ($r in $rows) {
    if ($n -eq 'artikel.csv') { "  Title='$($r.Title)' Handle='$($r.'URL handle')' Option1='$($r.'Option1 value')' Price=$($r.Price) Qty=$($r.'Inventory quantity') SKU=$($r.SKU) Desc='$($r.Description)' Bild='$($r.'Product image URL')'" }
    elseif ($n -eq 'kunden.csv') { "  '$($r.'First Name') $($r.'Last Name')' Adresse='$($r.'Default Address Address1')' PLZ=$($r.'Default Address Zip') Ort='$($r.'Default Address City')' Land=$($r.'Default Address Country Code')" }
    else { "  $($r.Name) $($r.'Financial Status') $($r.'Lineitem quantity') x '$($r.'Lineitem name')' $($r.'Lineitem price') EUR" }
  }
}
