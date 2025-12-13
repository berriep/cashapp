$content = Get-Content "C:\Users\bpeijmen\Documents\Code\BAI_Tool\Rabobank\UiPath\Autobank\Ouput\mt940 - generated NL31RABO0300087233 - 20251107.swi" -Raw -Encoding UTF8

Write-Host "File length: $($content.Length)"
Write-Host "First 200 chars:"
Write-Host $content.Substring(0, [Math]::Min(200, $content.Length))

# Test split
$blocks = $content -split "(?=:20:)"
Write-Host "`nBlocks found: $($blocks.Count)"

foreach ($i in 0..([Math]::Min(2, $blocks.Count-1))) {
    Write-Host "`nBlock $i (first 100 chars):"
    Write-Host $blocks[$i].Substring(0, [Math]::Min(100, $blocks[$i].Length))
}

# Check for :20: pattern
if ($content -match ":20:") {
    Write-Host "`n:20: pattern found in file"
} else {
    Write-Host "`n:20: pattern NOT found in file"
}
