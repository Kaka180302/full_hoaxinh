$rows = Import-Csv -Path .\sheet_products.csv -Encoding UTF8

function Normalize-Slug([string]$value) {
  $raw = ''
  if ($null -ne $value) { $raw = $value }
  $v = $raw.Trim().ToLowerInvariant()
  if ($v -like '*mỹ phẩm*' -or $v -like '*my pham*') { return 'mypham' }
  if ($v -like '*thực phẩm*' -or $v -like '*thuc pham*') { return 'thucpham' }
  return 'thietbi'
}

function Prefix([string]$slug) {
  switch ($slug) {
    'mypham' { 'MP' }
    'thucpham' { 'TP' }
    default { 'TB' }
  }
}

function SqlSafe([string]$text) {
  if ($null -eq $text) { return '' }
  $t = $text -replace "`r`n", ' '
  $t = $t -replace "`n", ' '
  $t = $t -replace '\s+', ' '
  $t = $t.Trim()
  return $t.Replace("'", "''")
}

$bySlugCounter = @{}
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('/* Seed products from Google Sheet */')
$lines.Add('')
$lines.Add('IF NOT EXISTS (SELECT 1 FROM dbo.Categories)')
$lines.Add('BEGIN')
$lines.Add("    INSERT INTO dbo.Categories (Name, Slug, IsActive)")
$lines.Add("    VALUES (N'Mỹ phẩm', N'mypham', 1),")
$lines.Add("           (N'Thực phẩm', N'thucpham', 1),")
$lines.Add("           (N'Thiết bị & gia dụng', N'thietbi', 1);")
$lines.Add('END')
$lines.Add('GO')
$lines.Add('')

$lines.Add('DECLARE @CategoryMap TABLE (Slug NVARCHAR(120) PRIMARY KEY, CategoryId INT NOT NULL);')
$lines.Add('INSERT INTO @CategoryMap(Slug, CategoryId)')
$lines.Add('SELECT Slug, Id FROM dbo.Categories WHERE Slug IN (N''mypham'', N''thucpham'', N''thietbi'');')
$lines.Add('')

foreach ($r in $rows) {
  $slug = Normalize-Slug $r.Category
  if (-not $bySlugCounter.ContainsKey($slug)) { $bySlugCounter[$slug] = 0 }
  $bySlugCounter[$slug]++
  $sku = ('{0}-{1:d4}' -f (Prefix $slug), $bySlugCounter[$slug])

  $name = SqlSafe $r.Name
  $image = SqlSafe $r.Image
  $summary = SqlSafe $r.Summary
  $desc = SqlSafe $r.Description
  $priceNum = 0
  [void][decimal]::TryParse(($r.Price -replace '[^0-9\.-]',''), [ref]$priceNum)

  $lines.Add("IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Sku = N'$sku')")
  $lines.Add('BEGIN')
  $lines.Add('    INSERT INTO dbo.Products')
  $lines.Add('    (Sku, Name, Price, StockQuantity, ImageUrl, Summary, Descriptions, CategoryId, IsActive)')
  $lines.Add("    SELECT N'$sku', N'$name', $priceNum, 100, N'$image', N'$summary', N'$desc', cm.CategoryId, 1")
  $lines.Add("    FROM @CategoryMap cm WHERE cm.Slug = N'$slug';")
  $lines.Add('END')
  $lines.Add('')
}

$lines.Add('GO')
$seedBlock = ($lines -join "`r`n")

$sqlPath = '.\Database\init_hoaxinhstore.sql'
$sql = Get-Content -Raw $sqlPath
$pattern = '(?s)/\* Seed products from Google Sheet \*/.*?GO\s*$'
$newSql = [regex]::Replace($sql, $pattern, $seedBlock)
Set-Content -Path $sqlPath -Encoding UTF8 -Value $newSql
