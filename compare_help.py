import json

with open('Aire/Translations/en.json', encoding='utf-8') as f:
    en = json.load(f)
with open('Aire/Translations/fr.json', encoding='utf-8') as f:
    fr = json.load(f)

en_help = en['help']
fr_help = fr['help']

print(f'English help count: {len(en_help)}')
print(f'French help count: {len(fr_help)}')

en_titles = [item.get('title', 'NO TITLE') for item in en_help]
fr_titles = [item.get('title', 'NO TITLE') for item in fr_help]

print('\nMissing in French:')
for title in en_titles:
    if title not in fr_titles:
        print(f'  - {title}')

print('\nExtra in French:')
for title in fr_titles:
    if title not in en_titles:
        print(f'  - {title}')