#!/usr/bin/env python3
"""
Extract English help content from en.json into a structured format suitable for translation.
Adds unique IDs, contextual notes, and a glossary of technical terms.
"""
import json
import re
import os
from pathlib import Path
from typing import Any, Dict, List, Set

# Paths
EN_JSON_PATH = Path("Aire/Translations/en.json")
OUTPUT_DIR = Path("translation_extraction")
EXTRACTION_PATH = OUTPUT_DIR / "help_structured_en.json"
GLOSSARY_PATH = OUTPUT_DIR / "help_glossary_detailed.json"

# Fields that should NOT be translated (structural)
STRUCTURAL_FIELDS = {"type", "action", "imagePath", "code", "nativeName", "flag"}
# Fields that contain nested arrays of strings that need translation
ARRAY_FIELDS = {"links", "cols", "rows"}

def load_json(path: Path) -> Any:
    """Load JSON, handling UTF-8 BOM."""
    with open(path, encoding="utf-8-sig") as f:
        return json.load(f)

def extract_placeholders(text: str) -> List[str]:
    """Return list of placeholders like {0}, {1}, etc."""
    return re.findall(r'\{(\d+)\}', text)

def generate_translator_notes(item: Dict) -> Dict[str, str]:
    """Generate contextual notes for translators for each field."""
    notes = {}
    # Title: usually a short heading
    if "title" in item:
        notes["title"] = "Short heading for this help item. Keep concise."
    # Content: main text, may contain bullet points, placeholders, and markdown-like formatting.
    if "content" in item:
        content = item["content"]
        placeholders = extract_placeholders(content)
        if placeholders:
            notes["content"] = f"Contains placeholders {', '.join('{' + p + '}' for p in placeholders)}. Keep them unchanged."
        else:
            notes["content"] = "Main explanatory text. May contain bullet points (•) and line breaks."
    # Image caption: description of an image
    if "imageCaption" in item:
        notes["imageCaption"] = "Caption for an image shown in the help window."
    # Intro: introductory text before a table
    if "intro" in item:
        notes["intro"] = "Introductory text above a table."
    # Links: each link has a label (clickable text) and an action (internal or URL)
    if "links" in item:
        notes["links"] = "List of hyperlinks. Translate the 'label' field; keep 'action' unchanged (internal command or URL)."
    # Cols: table column headers
    if "cols" in item:
        notes["cols"] = "Table column headers. Keep short and consistent."
    # Rows: table rows, each row is an array of strings
    if "rows" in item:
        notes["rows"] = "Table rows. Each cell may contain text, placeholders, or formatting."
    # Tab: section tab name (translatable, keep consistent)
    if "tab" in item:
        notes["tab"] = "Tab category name. Keep consistent across all help items belonging to same tab."
    # Type: content type (text, table, code) – structural
    if "type" in item:
        notes["type"] = "Content type: 'text', 'table', or 'code'. Do not translate."
    return notes

def enhance_item(item: Dict, idx: int) -> Dict:
    """Add ID and translator notes to an item."""
    enhanced = item.copy()
    enhanced["id"] = idx
    # Add translator notes as a separate object
    enhanced["translator_notes"] = generate_translator_notes(item)
    return enhanced

def extract_glossary(items: List[Dict]) -> Dict[str, str]:
    """Extract technical terms and provide definitions."""
    # Predefined glossary based on domain knowledge
    glossary = {
        "Aire": "The name of the application (do not translate).",
        "AI": "Artificial Intelligence (may be kept as AI).",
        "API": "Application Programming Interface (keep as API).",
        "API key": "Secret token used to authenticate with an AI provider.",
        "provider": "AI service such as OpenAI, Anthropic, Google AI, Ollama, etc.",
        "model": "Specific AI model like GPT-4o, Claude 3.5 Sonnet, etc.",
        "tool": "An action the AI can request Aire to perform (e.g., read file, browse web).",
        "MCP": "Model Context Protocol - a protocol for connecting external services.",
        "Ollama": "Local AI server for running models on your own machine.",
        "Claude.ai": "Anthropic's web interface for Claude models.",
        "Gemini": "Google's family of AI models.",
        "Local API": "Aire's internal API allowing other local apps to control it.",
        "voice output": "Text-to-speech feature that reads AI responses aloud.",
        "voice input": "Speech recognition that converts spoken words to text.",
        "system tray": "Area near the clock where Aire runs in the background.",
        "auto‑accept": "Profile that automatically approves certain AI actions.",
        "tool categories": "Groups of tools like Files, Browser, Mouse, etc.",
        "capability tests": "Checks to see what a provider/model can do.",
        "custom model": "User-defined JSON configuration for a model's capabilities.",
        "native function calling": "Structured tool calling via provider API.",
        "text‑based tool calling": "Tool calls embedded in text using XML/JSON blocks.",
        "placeholder": "Variable like {0} that will be replaced with a value at runtime.",
        "bullets": "Bullet points (•) used for lists.",
        "Windows": "Microsoft Windows operating system.",
        "Whisper": "OpenAI's speech recognition model.",
        "Node.js": "JavaScript runtime environment.",
        "npm": "Node package manager.",
        "PowerShell": "Windows scripting language.",
        "Python": "Programming language.",
        "JSON": "JavaScript Object Notation (data format).",
        "XML": "Extensible Markup Language.",
        "URL": "Uniform Resource Locator (web address).",
        "TCP": "Transmission Control Protocol.",
        "port": "Network port number.",
        "token": "A piece of data used for authentication.",
        "quota": "Usage limit imposed by an AI provider.",
        "rate limit": "Restriction on how many requests can be made in a period.",
        "self‑hosted": "Running software on your own server.",
        "offline": "Without an internet connection.",
        "privacy": "Keeping data private.",
        "integration": "Connecting different systems together.",
        "health check": "A test to verify a service is working.",
    }
    # Additional terms extracted from content (optional)
    # We could scan all text fields for capitalized technical terms, but for now we rely on predefined.
    return glossary

def main():
    OUTPUT_DIR.mkdir(exist_ok=True)
    
    print(f"Loading {EN_JSON_PATH}")
    data = load_json(EN_JSON_PATH)
    help_items = data.get("help", [])
    print(f"Found {len(help_items)} help items.")
    
    # Enhance each item
    enhanced_items = [enhance_item(item, i) for i, item in enumerate(help_items)]
    
    # Create final extraction structure
    extraction = {
        "metadata": {
            "source_file": str(EN_JSON_PATH),
            "extraction_date": "2026-04-05",
            "total_items": len(enhanced_items),
            "structural_fields": list(STRUCTURAL_FIELDS),
            "array_fields": list(ARRAY_FIELDS),
            "note": "Fields marked as structural should not be translated. Placeholders like {0} must be preserved.",
        },
        "items": enhanced_items,
    }
    
    # Write extraction
    with open(EXTRACTION_PATH, "w", encoding="utf-8") as f:
        json.dump(extraction, f, ensure_ascii=False, indent=2)
    print(f"Structured extraction saved to {EXTRACTION_PATH}")
    
    # Generate glossary
    glossary = extract_glossary(help_items)
    glossary_data = {
        "metadata": {
            "generated_date": "2026-04-05",
            "purpose": "Ensure consistent translation of technical terms across languages.",
        },
        "terms": glossary,
    }
    with open(GLOSSARY_PATH, "w", encoding="utf-8") as f:
        json.dump(glossary_data, f, ensure_ascii=False, indent=2)
    print(f"Glossary saved to {GLOSSARY_PATH}")

if __name__ == "__main__":
    main()