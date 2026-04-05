#!/usr/bin/env python3
"""
Pull all STS2 data from spire-codex.com API and save to skill reference files.
"""

import json
import urllib.request
import csv
import os
import time

API_BASE = "https://spire-codex.com/api"
SKILL_DIR = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "references")

def fetch_json(endpoint):
    """Fetch JSON data from API."""
    url = f"{API_BASE}/{endpoint}"
    print(f"Fetching {url}...")
    
    req = urllib.request.Request(url, headers={
        'User-Agent': 'Mozilla/5.0 (compatible; OpenClaw/1.0)',
        'Accept': 'application/json',
    })
    
    time.sleep(0.5)  # Rate limiting
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.loads(r.read().decode('utf-8'))

def save_csv(filename, rows, fieldnames):
    """Save data to CSV file."""
    filepath = os.path.join(SKILL_DIR, filename)
    with open(filepath, 'w', newline='', encoding='utf-8') as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames)
        writer.writeheader()
        for row in rows:
            writer.writerow(row)
    print(f"Saved {len(rows)} rows to {filepath}")

def main():
    # Ensure directory exists
    os.makedirs(SKILL_DIR, exist_ok=True)
    
    # Character mapping
    CHARACTERS = ['ironclad', 'silent', 'defect', 'necrobinder', 'regent']
    
    # ========== CARDS ==========
    print("\n=== FETCHING CARDS ===")
    cards_data = fetch_json("cards")
    
    # Filter out status/curse/token cards for character files
    for char in CHARACTERS:
        char_cards = [c for c in cards_data if c.get('color', '').lower() == char]
        
        # Prepare rows with relevant fields
        rows = []
        for c in char_cards:
            rows.append({
                'name': c.get('name', ''),
                'id': c.get('id', ''),
                'cost': str(c.get('cost', '?')),
                'type': c.get('type', ''),
                'rarity': c.get('rarity', ''),
                'description': c.get('description', ''),
                'keywords': '|'.join(c.get('keywords') or []),
                'tags': '|'.join(c.get('tags') or []),
                'damage': str(c.get('damage') or ''),
                'block': str(c.get('block') or ''),
                'upgrade': json.dumps(c.get('upgrade') or {}),
            })
        
        save_csv(f"{char}-cards.csv", rows, 
                 ['name', 'id', 'cost', 'type', 'rarity', 'description', 'keywords', 'tags', 'damage', 'block', 'upgrade'])
    
    # Colorless cards
    colorless = [c for c in cards_data if c.get('color', '').lower() == 'colorless' 
                 and c.get('rarity') not in ['Token', 'Status', 'Curse']]
    rows = []
    for c in colorless:
        rows.append({
            'name': c.get('name', ''),
            'id': c.get('id', ''),
            'cost': str(c.get('cost', '?')),
            'type': c.get('type', ''),
            'rarity': c.get('rarity', ''),
            'description': c.get('description', ''),
            'keywords': '|'.join(c.get('keywords') or []),
            'upgrade': json.dumps(c.get('upgrade') or {}),
        })
    save_csv("colorless-cards.csv", rows,
             ['name', 'id', 'cost', 'type', 'rarity', 'description', 'keywords', 'upgrade'])
    
    # Status/Curse cards (shared)
    status_curse = [c for c in cards_data if c.get('rarity') in ['Status', 'Curse']]
    rows = []
    for c in status_curse:
        rows.append({
            'name': c.get('name', ''),
            'id': c.get('id', ''),
            'type': c.get('type', ''),
            'description': c.get('description', ''),
            'keywords': '|'.join(c.get('keywords') or []),
        })
    save_csv("status-curse-cards.csv", rows,
             ['name', 'id', 'type', 'description', 'keywords'])
    
    # Token cards
    token_cards = [c for c in cards_data if c.get('rarity') == 'Token']
    rows = []
    for c in token_cards:
        rows.append({
            'name': c.get('name', ''),
            'id': c.get('id', ''),
            'cost': str(c.get('cost', '?')),
            'type': c.get('type', ''),
            'description': c.get('description', ''),
            'keywords': '|'.join(c.get('keywords') or []),
        })
    save_csv("token-cards.csv", rows,
             ['name', 'id', 'cost', 'type', 'description', 'keywords'])
    
    # Event cards
    event_cards = [c for c in cards_data if c.get('color', '').lower() == 'event' 
                   or c.get('rarity') == 'Event' or c.get('rarity') == 'Ancient']
    rows = []
    for c in event_cards:
        rows.append({
            'name': c.get('name', ''),
            'id': c.get('id', ''),
            'cost': str(c.get('cost', '?')),
            'type': c.get('type', ''),
            'rarity': c.get('rarity', ''),
            'description': c.get('description', ''),
            'keywords': '|'.join(c.get('keywords') or []),
        })
    save_csv("event-ancient-cards.csv", rows,
             ['name', 'id', 'cost', 'type', 'rarity', 'description', 'keywords'])
    
    # ========== RELICS ==========
    print("\n=== FETCHING RELICS ===")
    relics_data = fetch_json("relics")
    
    rows = []
    for r in relics_data:
        rows.append({
            'name': r.get('name', ''),
            'id': r.get('id', ''),
            'rarity': r.get('rarity', ''),
            'character': r.get('character') or 'Shared',
            'description': r.get('description', ''),
            'pool': r.get('pool', ''),
        })
    save_csv("relics.csv", rows,
             ['name', 'id', 'rarity', 'character', 'description', 'pool'])
    
    # ========== POTIONS ==========
    print("\n=== FETCHING POTIONS ===")
    potions_data = fetch_json("potions")
    
    rows = []
    for p in potions_data:
        rows.append({
            'name': p.get('name', ''),
            'id': p.get('id', ''),
            'rarity': p.get('rarity', ''),
            'character': p.get('character') or 'Shared',
            'description': p.get('description', ''),
        })
    save_csv("potions.csv", rows,
             ['name', 'id', 'rarity', 'character', 'description'])
    
    # ========== MONSTERS ==========
    print("\n=== FETCHING MONSTERS ===")
    monsters_data = fetch_json("monsters")
    
    rows = []
    for m in monsters_data:
        rows.append({
            'name': m.get('name', ''),
            'id': m.get('id', ''),
            'hp_min': m.get('hp_min', ''),
            'hp_max': m.get('hp_max', ''),
            'type': m.get('type', ''),
            'act': m.get('act', ''),
        })
    save_csv("monsters.csv", rows,
             ['name', 'id', 'hp_min', 'hp_max', 'type', 'act'])
    
    # ========== KEYWORDS ==========
    print("\n=== FETCHING KEYWORDS ===")
    keywords_data = fetch_json("keywords")
    
    rows = []
    for k in keywords_data:
        rows.append({
            'name': k.get('name', ''),
            'id': k.get('id', ''),
            'description': k.get('description', ''),
        })
    save_csv("keywords.csv", rows,
             ['name', 'id', 'description'])
    
    # ========== ENCHANTMENTS ==========
    print("\n=== FETCHING ENCHANTMENTS ===")
    enchantments_data = fetch_json("enchantments")
    
    rows = []
    for e in enchantments_data:
        rows.append({
            'name': e.get('name', ''),
            'id': e.get('id', ''),
            'description': e.get('description', ''),
            'card_type': e.get('card_type') or 'Any',
            'stackable': str(e.get('stackable', '')),
        })
    save_csv("enchantments.csv", rows,
             ['name', 'id', 'description', 'card_type', 'stackable'])
    
    # ========== SUMMARY ==========
    print("\n=== SUMMARY ===")
    print(f"Total cards: {len(cards_data)}")
    for char in CHARACTERS:
        count = len([c for c in cards_data if c.get('color', '').lower() == char])
        print(f"  {char.title()}: {count}")
    print(f"  Colorless: {len(colorless)}")
    print(f"  Status/Curse: {len(status_curse)}")
    print(f"  Token: {len(token_cards)}")
    print(f"  Event/Ancient: {len(event_cards)}")
    print(f"Total relics: {len(relics_data)}")
    print(f"Total potions: {len(potions_data)}")
    print(f"Total monsters: {len(monsters_data)}")
    print(f"Total keywords: {len(keywords_data)}")
    print(f"Total enchantments: {len(enchantments_data)}")
    
    print("\n✅ Done! Files saved to:", SKILL_DIR)

if __name__ == "__main__":
    main()
