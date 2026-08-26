[CmdletBinding()]
param(
    [string]$SkillRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$failures = [System.Collections.Generic.List[string]]::new()

function Add-Failure {
    param([string]$Message)
    $script:failures.Add($Message)
}

$requiredFiles = @(
    'SKILL.md',
    'agents/openai.yaml',
    'references/system-map.md',
    'references/feedback-incident.md',
    'references/authorization.md',
    'references/migrations.md',
    'references/integrations.md',
    'references/verification.md',
    'references/knowledge-maintenance.md',
    'scripts/validate-skill.ps1'
)

$resolvedRoot = [System.IO.Path]::GetFullPath($SkillRoot)
foreach ($relativePath in $requiredFiles) {
    $candidate = Join-Path $resolvedRoot $relativePath
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        Add-Failure "Missing required file: $relativePath"
    }
}

$skillPath = Join-Path $resolvedRoot 'SKILL.md'
if (Test-Path -LiteralPath $skillPath -PathType Leaf) {
    $skill = Get-Content -Raw -LiteralPath $skillPath
    $frontmatter = [regex]::Match($skill, '\A---\r?\n(?<yaml>.*?)\r?\n---\r?\n', [System.Text.RegularExpressions.RegexOptions]::Singleline)
    if (-not $frontmatter.Success) {
        Add-Failure 'SKILL.md must begin with a closed YAML frontmatter block.'
    }
    else {
        $yaml = $frontmatter.Groups['yaml'].Value
        if ($yaml -notmatch '(?m)^name:\s*urbanservice-change\s*$') {
            Add-Failure 'SKILL.md frontmatter name must be urbanservice-change.'
        }
        if ($yaml -notmatch '(?m)^description:\s*\S.+$') {
            Add-Failure 'SKILL.md frontmatter must contain a non-empty description.'
        }
    }

    if ($skill -match '(?i)\b(TODO|TBD|PLACEHOLDER)\b') {
        Add-Failure 'SKILL.md contains an unfinished scaffold marker.'
    }
}

$openAiPath = Join-Path $resolvedRoot 'agents/openai.yaml'
if (Test-Path -LiteralPath $openAiPath -PathType Leaf) {
    $openAi = Get-Content -Raw -LiteralPath $openAiPath
    foreach ($field in @('display_name', 'short_description', 'default_prompt')) {
        if ($openAi -notmatch "(?m)^\s{2}$field`:\s*`"[^`"]+`"\s*$") {
            Add-Failure "agents/openai.yaml is missing quoted interface.$field."
        }
    }
    if ($openAi -notmatch '(?m)^\s{2}default_prompt:\s*"[^"]*\$urbanservice-change[^"]*"\s*$') {
        Add-Failure 'interface.default_prompt must mention $urbanservice-change.'
    }
    if ($openAi -notmatch '(?m)^\s{2}allow_implicit_invocation:\s*true\s*$') {
        Add-Failure 'policy.allow_implicit_invocation must be true.'
    }
}

$markdownFiles = Get-ChildItem -LiteralPath $resolvedRoot -Recurse -File -Filter '*.md'
$linkPattern = [regex]'\[[^\]]+\]\((?<target>[^)]+)\)'
foreach ($markdownFile in $markdownFiles) {
    $content = Get-Content -Raw -LiteralPath $markdownFile.FullName
    foreach ($match in $linkPattern.Matches($content)) {
        $target = $match.Groups['target'].Value.Trim()
        if ($target -match '^(?:https?://|mailto:|#)') {
            continue
        }

        $pathOnly = ($target -split '#', 2)[0].Trim('<', '>')
        if ([string]::IsNullOrWhiteSpace($pathOnly)) {
            continue
        }

        $linkedPath = [System.IO.Path]::GetFullPath((Join-Path $markdownFile.DirectoryName $pathOnly))
        if (-not (Test-Path -LiteralPath $linkedPath)) {
            $relativeMarkdown = $markdownFile.FullName.Substring($resolvedRoot.Length).TrimStart('\', '/')
            Add-Failure "Broken relative link in ${relativeMarkdown}: $target"
        }
    }
}

if ($failures.Count -gt 0) {
    Write-Error ("UrbanService skill validation failed:`n- " + ($failures -join "`n- "))
    exit 1
}

Write-Host "UrbanService skill validation passed ($($requiredFiles.Count) required files, $($markdownFiles.Count) Markdown files)."
