#!/usr/bin/env python3
"""
Generate a Mermaid diagram of the STS2 map from save file data.
Outputs a mermaid.ink URL for the rendered image.

Usage:
    curl -s http://localhost:15526/state | python3 generate_map.py
"""

import sys
import json
import base64

def main():
    d = json.load(sys.stdin)
    act_idx = d.get('current_act_index', 0)
    act = d['acts'][act_idx]
    saved_map = act.get('saved_map', {})
    points = saved_map.get('points', [])
    visited = d.get('visited_map_coords', [])
    visited_set = {(v['row'], v['col']) for v in visited}
    
    # Build grid
    grid = {}
    for point in points:
        coord = point.get('coord', {})
        row, col = coord.get('row', 0), coord.get('col', 0)
        room_type = point.get('type', '?')
        children = [(c.get('row'), c.get('col')) for c in point.get('children', [])]
        grid[(row, col)] = {'type': room_type, 'children': children}
    
    # Symbols
    symbols = {
        'monster': '🗡️ M',
        'elite': '⚔️ ELITE',
        'rest_site': '🔥 REST',
        'shop': '💰 SHOP',
        'treasure': '📦 T',
        'unknown': '❓',
        'boss': '👑 BOSS'
    }
    
    def node_id(r, c):
        return f'R{r}C{c}'
    
    # Find current position
    current_pos = max(visited, key=lambda v: v['row']) if visited else None
    current_row = current_pos['row'] if current_pos else 0
    
    # Calculate recommended path
    def trace_paths(start, grid, path=None):
        if path is None:
            path = []
        if start not in grid:
            return [path]
        node = grid[start]
        new_path = path + [(start[0], start[1], node['type'])]
        if not node['children']:
            return [new_path]
        all_paths = []
        for child in node['children']:
            all_paths.extend(trace_paths(child, grid, new_path))
        return all_paths
    
    def score_path(path):
        elites = sum(1 for r, c, t in path if t == 'elite')
        rests = sum(1 for r, c, t in path if t == 'rest_site')
        shops = sum(1 for r, c, t in path if t == 'shop')
        early_elite = any(t == 'elite' for r, c, t in path if r <= 8)
        early_shop = any(t == 'shop' for r, c, t in path if r <= 5)
        return (-elites, -int(early_elite), rests, shops, int(early_shop))
    
    # Find best path from current position or start
    if current_pos and (current_pos['row'], current_pos['col']) in grid:
        start_node = (current_pos['row'], current_pos['col'])
    else:
        starts = [(r, c) for (r, c) in grid.keys() if r == 1]
        start_node = starts[0] if starts else None
    
    recommended_path = set()
    if start_node:
        paths = trace_paths(start_node, grid)
        if paths:
            best_path = max(paths, key=score_path)
            recommended_path = {(r, c) for r, c, t in best_path}
    
    # Generate Mermaid
    lines = ['flowchart BT']
    
    # Group nodes by row for subgraphs
    max_row = max(r for r, c in grid.keys()) if grid else 0
    
    for row in range(1, max_row + 1):
        nodes_in_row = [(r, c) for (r, c) in grid.keys() if r == row]
        if nodes_in_row:
            lines.append(f'    subgraph R{row}[" "]')
            for r, c in sorted(nodes_in_row, key=lambda x: x[1]):
                t = grid[(r, c)]['type']
                sym = symbols.get(t, '❓')
                nid = node_id(r, c)
                # Mark current position
                if current_pos and r == current_pos['row'] and c == current_pos['col']:
                    sym = '⭐ YOU'
                lines.append(f'        {nid}["{sym}"]')
            lines.append('    end')
    
    # Boss node
    boss = saved_map.get('boss', {})
    if boss:
        lines.append('    BOSS["👑 BOSS"]')
    
    # Connections
    for (r, c), node in grid.items():
        for child in node['children']:
            cr, cc = child
            lines.append(f'    {node_id(r, c)} --> {node_id(cr, cc)}')
    
    # Connect last row to boss
    for (r, c) in grid.keys():
        if r == max_row:
            lines.append(f'    {node_id(r, c)} --> BOSS')
    
    # Styles
    # Visited rooms - gray
    for v in visited:
        if not (current_pos and v['row'] == current_pos['row'] and v['col'] == current_pos['col']):
            lines.append(f'    style {node_id(v["row"], v["col"])} fill:#666,stroke:#333')
    
    # Current position - bright green
    if current_pos:
        lines.append(f'    style {node_id(current_pos["row"], current_pos["col"])} fill:#00ff00,stroke:#000,stroke-width:4px')
    
    # Recommended path - light green (excluding visited and current)
    for (r, c) in recommended_path:
        if (r, c) not in visited_set and not (current_pos and r == current_pos['row'] and c == current_pos['col']):
            lines.append(f'    style {node_id(r, c)} fill:#90EE90,stroke:#228B22,stroke-width:2px')
    
    # Room type colors (for non-recommended, non-visited)
    for (r, c), node in grid.items():
        if (r, c) in visited_set or (r, c) in recommended_path:
            continue
        if current_pos and r == current_pos['row'] and c == current_pos['col']:
            continue
        t = node['type']
        if t == 'elite':
            lines.append(f'    style {node_id(r, c)} fill:#ff4444,stroke:#000')
        elif t == 'shop':
            lines.append(f'    style {node_id(r, c)} fill:#ffeb3b,stroke:#000')
        elif t == 'rest_site':
            lines.append(f'    style {node_id(r, c)} fill:#ff9800,stroke:#000')
        elif t == 'treasure':
            lines.append(f'    style {node_id(r, c)} fill:#9c27b0,stroke:#000')
    
    # Join and encode
    mermaid_code = '\n'.join(lines)
    encoded = base64.b64encode(mermaid_code.encode()).decode().replace('+', '-').replace('/', '_')
    
    print(f'https://mermaid.ink/img/{encoded}')
    
    # Also print analysis
    print()
    print('---')
    print(f'📍 **Current Position:** Row {current_pos["row"]}, Col {current_pos["col"]}' if current_pos else '📍 **Position:** Start of run')
    
    if recommended_path:
        path_types = [grid[(r,c)]['type'] for r, c in sorted(recommended_path, key=lambda x: x[0])]
        elites = sum(1 for t in path_types if t == 'elite')
        rests = sum(1 for t in path_types if t == 'rest_site')
        shops = sum(1 for t in path_types if t == 'shop')
        print(f'✅ **Recommended Path:** {elites} Elite(s), {rests} Rest(s), {shops} Shop(s)')

if __name__ == '__main__':
    main()
