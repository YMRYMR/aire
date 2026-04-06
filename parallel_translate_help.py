#!/usr/bin/env python3
"""
Parallel translation system for Aire help content.

Translates English help content to multiple languages using configurable
translation providers (deepseek-reasoner via agent system, fallback, etc.).
Handles parallel processing, terminology consistency, and formatting preservation.

Usage:
    python parallel_translate_help.py [--langs ar,de,es,...] [--provider deepseek|fallback] [--max-workers 2]
"""

import argparse
import json
import os
import re
import sys
from concurrent.futures import ThreadPoolExecutor, as_completed
from typing import Dict, List, Any, Optional, Tuple
from dataclasses import dataclass
import logging

# Configure logging
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')
logger = logging.getLogger(__name__)

# Paths
TRANSLATIONS_DIR = "Aire/Translations"
EXTRACTION_DIR = "translation_extraction"
ENGLISH_HELP_FILE = os.path.join(EXTRACTION_DIR, "help_structured_en.json")

# Languages to translate (remaining 10 languages)
DEFAULT_LANGUAGES = ["ar", "de", "es", "fr", "hi", "it", "ja", "ko", "pt-br", "uk"]

# Structural fields that should not be translated (from metadata)
STRUCTURAL_FIELDS = {"tab", "code", "imagePath", "action", "type", "nativeName", "flag"}
# Array fields that contain translatable strings within nested structures
ARRAY_FIELDS = {"cols", "rows", "links"}

# Glossary for consistent terminology (loaded from glossary file)
GLOSSARY_FILE = os.path.join(EXTRACTION_DIR, "help_glossary_en.json")
TERMINOLOGY_GLOSSARY = {}
if os.path.exists(GLOSSARY_FILE):
    with open(GLOSSARY_FILE, 'r', encoding='utf-8-sig') as f:
        glossary_data = json.load(f)
        TERMINOLOGY_GLOSSARY = glossary_data.get('terms', {})
else:
    logger.warning(f"Glossary file {GLOSSARY_FILE} not found, using empty glossary")
    TERMINOLOGY_GLOSSARY = {}

class QualityChecker:
    """Performs quality checks on translated content."""
    def __init__(self, glossary: Dict[str, str]):
        self.glossary = glossary
        # Build a list of glossary terms sorted by length descending to match longest first
        self.terms = sorted(glossary.keys(), key=len, reverse=True)
    
    def check_placeholders(self, original: str, translated: str) -> List[str]:
        """Verify that placeholders like {0} are preserved."""
        original_placeholders = set(re.findall(r'\{(\d+)\}', original))
        translated_placeholders = set(re.findall(r'\{(\d+)\}', translated))
        missing = original_placeholders - translated_placeholders
        extra = translated_placeholders - original_placeholders
        errors = []
        if missing:
            errors.append(f"Missing placeholders: {missing}")
        if extra:
            errors.append(f"Extra placeholders: {extra}")
        return errors
    
    def check_terminology(self, original: str, translated: str, lang_code: str) -> List[str]:
        """Check if glossary terms are incorrectly translated.
        Warn if a glossary term present in original is missing in translation."""
        warnings = []
        original_lower = original.lower()
        translated_lower = translated.lower()
        for term in self.terms:
            # Check if term appears in original (case-insensitive)
            if term.lower() in original_lower:
                # Check if same term appears in translated text
                if term.lower() not in translated_lower:
                    # Term may have been translated; warn for review
                    warnings.append(f"Glossary term '{term}' present in original but missing in translation")
                # If term appears but changed case? ignore
        return warnings
    
    def check_translation(self, original: str, translated: str, lang_code: str) -> Dict[str, List[str]]:
        """Run all quality checks for a single text."""
        errors = self.check_placeholders(original, translated)
        warnings = self.check_terminology(original, translated, lang_code)
        return {'errors': errors, 'warnings': warnings}

@dataclass
class TranslationItem:
    """Represents a single help item with its translatable fields."""
    id: int
    original: Dict[str, Any]
    # Fields to translate
    title: str
    content: str
    imageCaption: Optional[str]
    intro: Optional[str]
    links: List[Dict[str, str]]  # each dict has "label" (translatable) and "action" (structural)
    cols: List[str]  # column headers
    rows: List[List[str]]  # table rows

class TranslationProvider:
    """Base class for translation providers."""
    def translate(self, text: str, target_lang: str, source_lang: str = "en") -> str:
        raise NotImplementedError

class FallbackTranslationProvider(TranslationProvider):
    """Fallback provider that returns the original text (for testing)."""
    def translate(self, text: str, target_lang: str, source_lang: str = "en") -> str:
        logger.warning(f"Fallback translation for {target_lang}: returning original text")
        return text

class DeepSeekReasonerProvider(TranslationProvider):
    """Translation provider using deepseek-reasoner model via OpenAI-compatible API."""
    def __init__(self, api_key: Optional[str] = None, base_url: str = "https://api.deepseek.com"):
        try:
            import openai
        except ImportError:
            raise ImportError("OpenAI library not installed. Install with 'pip install openai'")
        self.client = openai.OpenAI(api_key=api_key, base_url=base_url)
        self.model = "deepseek-reasoner"
    
    def translate(self, text: str, target_lang: str, source_lang: str = "en") -> str:
        # Construct prompt for translation
        prompt = f"""Translate the following English text to {target_lang}.
Preserve any placeholders like {{0}}, {{1}}, etc. exactly as they appear.
Do not translate proper nouns (e.g., Aire, OpenAI, Anthropic).
Return only the translated text.

English: {text}
Translation:"""
        try:
            response = self.client.chat.completions.create(
                model=self.model,
                messages=[{"role": "user", "content": prompt}],
                temperature=0.1,
                max_tokens=len(text) * 2,
            )
            translated = response.choices[0].message.content.strip()
            # Remove any surrounding quotes
            translated = translated.strip('"\'')
            return translated
        except Exception as e:
            logger.error(f"DeepSeek translation failed: {e}")
            raise

class TranslationPipeline:
    def __init__(self, provider: TranslationProvider, max_workers: int = 2, enable_quality_checks: bool = True):
        self.provider = provider
        self.max_workers = max_workers
        self.quality_checker = QualityChecker(TERMINOLOGY_GLOSSARY) if enable_quality_checks else None
        self.english_items = self._load_english_items()
    
    def _load_english_items(self) -> List[TranslationItem]:
        """Load and parse English help structured data."""
        with open(ENGLISH_HELP_FILE, 'r', encoding='utf-8-sig') as f:
            data = json.load(f)
        items = []
        for item_dict in data['items']:
            # Ensure id exists; if not, generate from index
            item_id = item_dict.get('id', len(items))
            # Extract translatable fields
            title = item_dict.get('title', '')
            content = item_dict.get('content', '')
            imageCaption = item_dict.get('imageCaption')
            intro = item_dict.get('intro')
            links = item_dict.get('links', [])
            cols = item_dict.get('cols', [])
            rows = item_dict.get('rows', [])
            items.append(TranslationItem(
                id=item_id,
                original=item_dict,
                title=title,
                content=content,
                imageCaption=imageCaption,
                intro=intro,
                links=links,
                cols=cols,
                rows=rows
            ))
        logger.info(f"Loaded {len(items)} English help items")
        return items
    
    def _preserve_placeholders(self, text: str) -> Tuple[str, Dict[str, str]]:
        """Replace placeholders like {0}, {1} with temporary tokens."""
        placeholders = {}
        def replace(match):
            ph = f"__PLACEHOLDER_{len(placeholders)}__"
            placeholders[ph] = match.group(0)
            return ph
        # Match {0}, {1}, etc. but not escaped {{ }}
        protected = re.sub(r'\{(\d+)\}', replace, text)
        return protected, placeholders
    
    def _restore_placeholders(self, text: str, placeholders: Dict[str, str]) -> str:
        """Restore temporary tokens to original placeholders."""
        for ph, original in placeholders.items():
            text = text.replace(ph, original)
        return text
    
    def _translate_text(self, text: str, target_lang: str) -> str:
        """Translate a single text string, preserving placeholders."""
        if not text or not text.strip():
            return text
        protected, placeholders = self._preserve_placeholders(text)
        translated = self.provider.translate(protected, target_lang)
        restored = self._restore_placeholders(translated, placeholders)
        return restored
    
    def _translate_links(self, links: List[Dict[str, str]], target_lang: str) -> List[Dict[str, str]]:
        """Translate link labels, keep actions unchanged."""
        translated_links = []
        for link in links:
            label = link.get('label', '')
            action = link.get('action', '')
            translated_label = self._translate_text(label, target_lang) if label else ''
            translated_links.append({'label': translated_label, 'action': action})
        return translated_links
    
    def _translate_array(self, array: List[str], target_lang: str) -> List[str]:
        """Translate each string in a flat array."""
        return [self._translate_text(s, target_lang) for s in array]
    
    def _translate_rows(self, rows: List[List[str]], target_lang: str) -> List[List[str]]:
        """Translate each cell in a 2D rows array."""
        return [[self._translate_text(cell, target_lang) for cell in row] for row in rows]
    
    def translate_item(self, item: TranslationItem, target_lang: str) -> Dict[str, Any]:
        """Translate a single help item to target language."""
        # Start with original structural fields (copy)
        translated = item.original.copy()
        # Translate title
        if item.title:
            translated['title'] = self._translate_text(item.title, target_lang)
        # Translate content
        if item.content:
            translated['content'] = self._translate_text(item.content, target_lang)
        # Translate imageCaption
        if item.imageCaption:
            translated['imageCaption'] = self._translate_text(item.imageCaption, target_lang)
        # Translate intro
        if item.intro:
            translated['intro'] = self._translate_text(item.intro, target_lang)
        # Translate links
        if item.links:
            translated['links'] = self._translate_links(item.links, target_lang)
        # Translate cols
        if item.cols:
            translated['cols'] = self._translate_array(item.cols, target_lang)
        # Translate rows
        if item.rows:
            translated['rows'] = self._translate_rows(item.rows, target_lang)
        # Quality checks
        if self.quality_checker:
            self._validate_item_translation(item, translated, target_lang)
        return translated
    
    def _validate_item_translation(self, original_item: TranslationItem, translated_dict: Dict[str, Any], target_lang: str):
        """Run quality checks on translated item."""
        if not self.quality_checker:
            return
        # Define mapping of field names to original text
        fields_to_check = [
            ('title', original_item.title),
            ('content', original_item.content),
            ('imageCaption', original_item.imageCaption),
            ('intro', original_item.intro),
        ]
        for field, original_text in fields_to_check:
            if original_text and field in translated_dict:
                translated_text = translated_dict[field]
                result = self.quality_checker.check_translation(original_text, translated_text, target_lang)
                for err in result['errors']:
                    logger.error(f"Quality error in {field} for {target_lang}: {err}")
                for warn in result['warnings']:
                    logger.warning(f"Quality warning in {field} for {target_lang}: {warn}")
        # Check links labels
        if original_item.links and 'links' in translated_dict:
            for i, (orig_link, trans_link) in enumerate(zip(original_item.links, translated_dict['links'])):
                if 'label' in orig_link and 'label' in trans_link:
                    result = self.quality_checker.check_translation(orig_link['label'], trans_link['label'], target_lang)
                    for err in result['errors']:
                        logger.error(f"Quality error in links[{i}] label for {target_lang}: {err}")
        # Check cols and rows similarly (optional)
        # ...

    def translate_language(self, lang_code: str) -> Tuple[str, List[Dict[str, Any]]]:
        """Translate all help items for a single language."""
        logger.info(f"Starting translation for {lang_code}")
        translated_items = []
        for item in self.english_items:
            translated = self.translate_item(item, lang_code)
            translated_items.append(translated)
        logger.info(f"Completed translation for {lang_code}, {len(translated_items)} items")
        return lang_code, translated_items
    
    def run_parallel(self, languages: List[str]) -> Dict[str, List[Dict[str, Any]]]:
        """Translate multiple languages in parallel."""
        results = {}
        with ThreadPoolExecutor(max_workers=self.max_workers) as executor:
            future_to_lang = {executor.submit(self.translate_language, lang): lang for lang in languages}
            for future in as_completed(future_to_lang):
                lang = future_to_lang[future]
                try:
                    lang_code, items = future.result()
                    results[lang_code] = items
                except Exception as e:
                    logger.error(f"Translation failed for {lang}: {e}")
        return results
    
    def update_translation_files(self, translated_data: Dict[str, List[Dict[str, Any]]], backup: bool = True):
        """Update each language's JSON file with translated help array."""
        for lang_code, help_items in translated_data.items():
            file_path = os.path.join(TRANSLATIONS_DIR, f"{lang_code}.json")
            if not os.path.exists(file_path):
                logger.warning(f"Translation file {file_path} does not exist, skipping")
                continue
            # Load existing translation file
            with open(file_path, 'r', encoding='utf-8') as f:
                data = json.load(f)
            # Backup original help if needed
            if backup and 'help' in data:
                backup_key = 'help_backup'
                data[backup_key] = data['help']
            # Update help array
            data['help'] = help_items
            # Write back
            with open(file_path, 'w', encoding='utf-8', newline='\n') as f:
                json.dump(data, f, ensure_ascii=False, indent=2)
            logger.info(f"Updated {file_path} with {len(help_items)} help items")

def main():
    parser = argparse.ArgumentParser(description="Parallel translation system for Aire help content")
    parser.add_argument('--langs', default=','.join(DEFAULT_LANGUAGES),
                        help='Comma-separated language codes to translate')
    parser.add_argument('--provider', choices=['deepseek', 'fallback'], default='fallback',
                        help='Translation provider to use')
    parser.add_argument('--max-workers', type=int, default=2,
                        help='Maximum number of parallel translation workers')
    parser.add_argument('--api-key', help='API key for DeepSeek provider')
    parser.add_argument('--base-url', default='https://api.deepseek.com',
                        help='Base URL for DeepSeek API')
    parser.add_argument('--dry-run', action='store_true',
                        help='Perform translation but do not write files')
    args = parser.parse_args()
    
    languages = [lang.strip() for lang in args.langs.split(',') if lang.strip()]
    logger.info(f"Target languages: {languages}")
    
    # Initialize translation provider
    if args.provider == 'deepseek':
        try:
            provider = DeepSeekReasonerProvider(api_key=args.api_key, base_url=args.base_url)
        except Exception as e:
            logger.error(f"Failed to initialize DeepSeek provider: {e}. Falling back to fallback provider.")
            provider = FallbackTranslationProvider()
    else:
        provider = FallbackTranslationProvider()
    
    # Create pipeline
    pipeline = TranslationPipeline(provider, max_workers=args.max_workers)
    
    # Run parallel translation
    translated_data = pipeline.run_parallel(languages)
    
    # Update files unless dry-run
    if not args.dry_run:
        pipeline.update_translation_files(translated_data)
        logger.info("Translation files updated successfully")
    else:
        logger.info("Dry-run completed, no files written")
    
    # Generate summary report
    for lang, items in translated_data.items():
        logger.info(f"{lang}: {len(items)} items translated")

if __name__ == '__main__':
    main()