# Extract English help content for translation
# Outputs a structured JSON file with IDs, context, and glossary

$enJsonPath = "Aire/Translations/en.json"
$outputPath = "translation_extraction/help_extraction_en.json"
$glossaryPath = "translation_extraction/help_glossary_en.json"

Write-Host "Reading $enJsonPath" -ForegroundColor Cyan
$json = Get-Content $enJsonPath -Raw | ConvertFrom-Json

$help = $json.help
Write-Host "Found $($help.Count) help items" -ForegroundColor Green

# Add unique IDs and context notes
$extracted = @()
$index = 0
foreach ($item in $help) {
    $obj = [PSCustomObject]@{
        id = $index
        tab = $item.tab
        type = $item.type
        title = $item.title
        content = $item.content
        # Optional fields (may be null)
        links = $item.links
        imagePath = $item.imagePath
        imageCaption = $item.imageCaption
        intro = $item.intro
        cols = $item.cols
        rows = $item.rows
        # Translator notes (to be filled manually)
        translator_notes = ""
    }
    # Remove empty fields to keep JSON clean
    $props = $obj.PSObject.Properties.Name | Where-Object { $obj.$_ -ne $null }
    $filtered = [PSCustomObject]@{}
    foreach ($prop in $props) {
        $filtered | Add-Member -MemberType NoteProperty -Name $prop -Value $obj.$prop
    }
    $extracted += $filtered
    $index++
}

# Create output directory if not exists
if (-not (Test-Path "translation_extraction")) {
    New-Item -ItemType Directory -Path "translation_extraction" | Out-Null
}

# Save extracted help
$extracted | ConvertTo-Json -Depth 10 | Set-Content $outputPath -Encoding UTF8
Write-Host "Extraction saved to $outputPath" -ForegroundColor Green

# Generate glossary of technical terms
$terms = @{
    "API" = "Application Programming Interface"
    "API key" = "Secret token used to authenticate with an AI provider"
    "provider" = "AI service such as OpenAI, Anthropic, Google AI, Ollama, etc."
    "model" = "Specific AI model like GPT-4o, Claude 3.5 Sonnet, etc."
    "tool" = "An action the AI can request Aire to perform (e.g., read file, browse web)"
    "MCP" = "Model Context Protocol - a protocol for connecting external services"
    "Ollama" = "Local AI server for running models on your own machine"
    "Claude.ai" = "Anthropic's web interface for Claude models"
    "Gemini" = "Google's family of AI models"
    "Local API" = "Aire's internal API allowing other local apps to control it"
    "voice output" = "Text-to-speech feature that reads AI responses aloud"
    "voice input" = "Speech recognition that converts spoken words to text"
    "system tray" = "Area near the clock where Aire runs in the background"
    "auto-accept" = "Profile that automatically approves certain AI actions"
    "tool categories" = "Groups of tools like Files, Browser, Mouse, etc."
    "capability tests" = "Checks to see what a provider/model can do"
    "custom model" = "User-defined JSON configuration for a model's capabilities"
    "native function calling" = "Structured tool calling via provider API"
    "text‑based tool calling" = "Tool calls embedded in text using XML/JSON blocks"
    "placeholder" = "Variable like {0} that will be replaced with a value at runtime"
}

$glossary = [PSCustomObject]@{
    generated_date = Get-Date -Format "yyyy-MM-dd"
    terms = $terms
}

$glossary | ConvertTo-Json -Depth 5 | Set-Content $glossaryPath -Encoding UTF8
Write-Host "Glossary saved to $glossaryPath" -ForegroundColor Green