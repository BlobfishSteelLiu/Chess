# 重新编译 Blob的国际象棋 安装包
#
# 重要：必须先把安装包编译到工作区**外面**的临时目录，再复制回 dist。
# 直接在 F:\VS_Projects 下生成会被文件锁/杀软干扰（ISCC 常要失败两三次，
# 而且即使"成功"也可能产出资源段损坏的 Setup.exe——运行时报
# “Setup was unable to create the directory ...is-XXXX.tmp，Error 5: 拒绝访问”）。

$ErrorActionPreference = 'Stop'

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$iss  = Join-Path $here 'installer.iss'
$dist = Join-Path $here 'dist'
$iscc = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'

if (-not (Test-Path $iscc)) { throw "找不到 Inno Setup 编译器：$iscc" }
if (-not (Test-Path $iss))  { throw "找不到脚本：$iss" }

$build = Join-Path $env:TEMP ('blobsetup-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Force -Path $build | Out-Null
Write-Host "编译到临时目录：$build"

$success = $false
for ($i = 1; $i -le 10; $i++) {
    & $iscc "/O$build" $iss
    if ($LASTEXITCODE -eq 0) { $success = $true; Write-Host "第 $i 次编译成功"; break }
    Write-Host "第 $i 次失败，重试…"
    Start-Sleep -Seconds 2
}
if (-not $success) { throw 'Inno Setup 编译失败' }

$built = Get-ChildItem $build -Filter '*.exe' | Select-Object -First 1
if (-not $built) { throw '没有找到编译产物' }

New-Item -ItemType Directory -Force -Path $dist | Out-Null
$target = Join-Path $dist $built.Name
Copy-Item -LiteralPath $built.FullName -Destination $target -Force

$sameHash = (Get-FileHash $built.FullName).Hash -eq (Get-FileHash $target).Hash
Remove-Item -Recurse -Force $build

Write-Host ""
Write-Host ("产物   ： {0}" -f $target)
Write-Host ("大小   ： {0:N0} 字节" -f (Get-Item $target).Length)
Write-Host ("校验   ： {0}" -f $(if ($sameHash) { '复制前后哈希一致' } else { '哈希不一致，请重试' }))
