#!/usr/bin/env python3
"""
Browser State Analyzer
Analyzes the JSON output from browser-inspector.js
"""

import json
import sys
from datetime import datetime
from collections import Counter

def analyze_console_logs(logs):
    """Analyze console logs"""
    print("\n📝 CONSOLE LOGS:")
    print("-" * 50)
    
    if not logs:
        print("No console logs found")
        return
    
    # Count by type
    log_types = Counter(log['type'] for log in logs)
    print(f"Total logs: {len(logs)}")
    for log_type, count in log_types.items():
        print(f"  {log_type}: {count}")
    
    # Show recent logs
    print("\nRecent logs:")
    for log in logs[-10:]:
        timestamp = log['timestamp'].split('T')[1].split('.')[0]
        print(f"  [{timestamp}] {log['type'].upper()}: {log['message'][:100]}")

def analyze_network_requests(requests):
    """Analyze network requests"""
    print("\n🌐 NETWORK REQUESTS:")
    print("-" * 50)
    
    if not requests:
        print("No network requests found")
        return
    
    print(f"Total requests: {len(requests)}")
    
    # Group by status
    status_codes = Counter(req.get('status', 'pending') for req in requests)
    print("\nBy status:")
    for status, count in status_codes.items():
        print(f"  {status}: {count}")
    
    # Show recent requests
    print("\nRecent requests:")
    for req in requests[-5:]:
        timestamp = req['timestamp'].split('T')[1].split('.')[0]
        status = req.get('status', 'pending')
        print(f"  [{timestamp}] {req['method']} {req['url'][:50]} - Status: {status}")

def analyze_errors(errors):
    """Analyze errors"""
    print("\n❌ ERRORS:")
    print("-" * 50)
    
    if not errors:
        print("No errors found ✅")
        return
    
    print(f"Total errors: {len(errors)}")
    
    for error in errors:
        timestamp = error['timestamp'].split('T')[1].split('.')[0]
        print(f"\n[{timestamp}] {error['message']}")
        if error.get('source'):
            print(f"  Source: {error['source']}:{error.get('line', '?')}")
        if error.get('stack'):
            print(f"  Stack: {error['stack'][:200]}...")

def analyze_forms(forms):
    """Analyze form data"""
    print("\n📋 FORMS:")
    print("-" * 50)
    
    if not forms:
        print("No forms found")
        return
    
    print(f"Total forms: {len(forms)}")
    
    for i, form in enumerate(forms):
        print(f"\nForm {i + 1}:")
        print(f"  ID: {form.get('id', 'none')}")
        print(f"  Action: {form.get('action', 'none')}")
        print(f"  Method: {form.get('method', 'GET')}")
        print(f"  Fields: {len(form.get('fields', {}))}")
        
        # Show field values
        for name, field in form.get('fields', {}).items():
            value = field.get('value', '')
            if value and field.get('type') != 'password':
                print(f"    {name}: {value[:50]}")

def analyze_dom(dom):
    """Analyze DOM snapshot"""
    print("\n🌳 DOM SNAPSHOT:")
    print("-" * 50)
    
    print(f"Total elements: {dom.get('totalElements', 0)}")
    print(f"Forms: {dom.get('forms', 0)}")
    print(f"Images: {dom.get('images', 0)}")
    print(f"Links: {dom.get('links', 0)}")
    print(f"Scripts: {dom.get('scripts', 0)}")
    print(f"Stylesheets: {dom.get('stylesheets', 0)}")
    
    # Analyze inputs
    inputs = dom.get('inputs', [])
    if inputs:
        print(f"\nInput fields: {len(inputs)}")
        input_types = Counter(inp['type'] for inp in inputs)
        for inp_type, count in input_types.items():
            print(f"  {inp_type}: {count}")
        
        # Show filled inputs
        filled_inputs = [inp for inp in inputs if inp.get('value') and inp['type'] != 'password']
        if filled_inputs:
            print("\nFilled inputs:")
            for inp in filled_inputs[:5]:
                print(f"  {inp.get('id') or inp.get('name', 'unnamed')}: {inp['value'][:30]}")

def main():
    if len(sys.argv) < 2:
        print("Usage: python analyze-browser-state.py <state.json>")
        print("Or pipe JSON: echo '{...}' | python analyze-browser-state.py -")
        sys.exit(1)
    
    # Read JSON
    if sys.argv[1] == '-':
        data = json.loads(sys.stdin.read())
    else:
        with open(sys.argv[1], 'r') as f:
            data = json.load(f)
    
    # Header
    print("=" * 60)
    print("🔍 BROWSER STATE ANALYSIS")
    print("=" * 60)
    print(f"URL: {data.get('url', 'unknown')}")
    print(f"Title: {data.get('title', 'unknown')}")
    print(f"Timestamp: {data.get('timestamp', 'unknown')}")
    
    # Viewport
    viewport = data.get('viewport', {})
    print(f"Viewport: {viewport.get('width')}x{viewport.get('height')}")
    print(f"Scroll: X={viewport.get('scrollX')}, Y={viewport.get('scrollY')}")
    
    # Analyze each section
    analyze_console_logs(data.get('consoleLogs', []))
    analyze_network_requests(data.get('networkRequests', []))
    analyze_errors(data.get('errors', []))
    analyze_dom(data.get('dom', {}))
    analyze_forms(data.get('forms', []))
    
    # Storage summary
    print("\n💾 STORAGE:")
    print("-" * 50)
    local_storage = data.get('localStorage', {})
    session_storage = data.get('sessionStorage', {})
    print(f"LocalStorage items: {len(local_storage)}")
    print(f"SessionStorage items: {len(session_storage)}")
    
    # Summary
    print("\n" + "=" * 60)
    print("📊 SUMMARY:")
    print(f"✓ Console logs: {len(data.get('consoleLogs', []))}")
    print(f"✓ Network requests: {len(data.get('networkRequests', []))}")
    print(f"✓ Errors: {len(data.get('errors', []))}")
    print(f"✓ Forms: {len(data.get('forms', []))}")
    print("=" * 60)

if __name__ == "__main__":
    main()