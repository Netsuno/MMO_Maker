# Prepares DA capture folders for Frog.Client HUD (Windows). Does not launch WinForms.
# Linux cannot produce these PNGs — compile-only.
param(
    [string]$Root = $(Join-Path (Split-Path -Parent $PSScriptRoot) "artifacts/client-ui-modernization")
)

$sets = @(
    "1280-dpi100",
    "1920-dpi100",
    "1280-dpi125",
    "1920-dpi125"
)
$shots = @(
    "01-shell-hud.png",
    "02-status-combat.png",
    "03-minimap-quest.png",
    "04-chat-channels.png",
    "05-hotbar-menu.png",
    "06-inventory-gold.png",
    "07-options-reseau.png"
)

New-Item -ItemType Directory -Force -Path $Root | Out-Null
foreach ($set in $sets) {
    $dir = Join-Path $Root $set
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $readme = Join-Path $dir "README.txt"
    @"
FRoG client UI DA captures — $set
Window: $($set.Split('-')[0])  DPI: $($set.Split('-')[1])
Fill: $($shots -join ', ')
Do not overwrite Phase 8 exact-sha Dialogue/Quest/Environment panels.
See docs/progress/client-ui-modernization/CAPTURES.md
"@ | Set-Content -Path $readme -Encoding utf8
}

Write-Host "Folders ready under $Root"
Write-Host "Set Windows display scale, then ClientSize 1280x720 or 1920x1080, Playing phase, tabs closed unless shot 06."
Write-Host "Linux agents cannot run this GUI — structural review only until these PNGs exist."
