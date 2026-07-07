param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateNotNullOrEmpty()]
    [string]$Message,

    [Parameter(Position = 1)]
    [string[]]$Paths = @(),

    [switch]$All,
    [switch]$NoPush,
    [string]$Remote = "origin",
    [string]$Branch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-Git
{
    param(
        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]]$GitArgs
    )

    & git @GitArgs
    if ($LASTEXITCODE -ne 0)
    {
        throw "git $($GitArgs -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Test-GitSuccess
{
    param(
        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]]$GitArgs
    )

    & git @GitArgs *> $null
    return $LASTEXITCODE -eq 0
}

if (-not (Test-GitSuccess rev-parse --is-inside-work-tree))
{
    throw "Current directory is not inside a git repository."
}

$repoRoot = (& git rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repoRoot))
{
    throw "Cannot resolve git repository root."
}

Set-Location $repoRoot

if ($All -and $Paths.Count -gt 0)
{
    throw "Use either -All or explicit paths, not both."
}

if ($All)
{
    Invoke-Git add -A -- .
}
elseif ($Paths.Count -gt 0)
{
    Invoke-Git add -- @Paths
}

if (Test-GitSuccess diff --cached --quiet)
{
    throw "No staged changes to commit. Stage files first, pass paths, or use -All."
}

Invoke-Git commit -m $Message

if ($NoPush)
{
    Write-Host "Commit created. Push skipped because -NoPush was set."
    exit 0
}

if ([string]::IsNullOrWhiteSpace($Branch))
{
    $Branch = (& git branch --show-current).Trim()
}

if ([string]::IsNullOrWhiteSpace($Branch))
{
    throw "Cannot determine current branch. Pass -Branch explicitly."
}

Invoke-Git push -u $Remote $Branch
Write-Host "Committed and pushed to $Remote/$Branch."
