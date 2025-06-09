#!/usr/bin/env python3
"""
OptimalyAI Development Runner
- Automaticky restartuje aplikaci
- Běží v pozadí (detached)
- Inteligentní správa Chrome tabů
- Podporuje restart příkazem
"""

import subprocess
import sys
import os
import time
import socket
import signal
import threading
import datetime

class DevRunner:
    def __init__(self):
        self.port = 5005
        self.url = f"https://localhost:{self.port}"
        self.process = None
        self.log_file = f"dev-runner-{datetime.datetime.now().strftime('%Y%m%d-%H%M%S')}.log"
        
    def is_port_in_use(self):
        """Kontrola jestli port není obsazený"""
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
            return s.connect_ex(('localhost', self.port)) == 0
    
    def find_chrome_tab(self):
        """Najde Chrome tab s aplikací"""
        applescript = f'''
        tell application "Google Chrome"
            repeat with w in windows
                set tabIndex to 0
                repeat with t in tabs of w
                    set tabIndex to tabIndex + 1
                    if URL of t contains "localhost:{self.port}" then
                        set active tab index of w to tabIndex
                        set index of w to 1
                        activate
                        return true
                    end if
                end repeat
            end repeat
            return false
        end tell
        '''
        
        try:
            result = subprocess.run(['osascript', '-e', applescript], 
                                  capture_output=True, text=True)
            return result.stdout.strip() == 'true'
        except:
            return False
    
    def refresh_chrome(self):
        """Refreshne Chrome tab"""
        applescript = '''
        tell application "Google Chrome"
            tell front window
                reload active tab
            end tell
        end tell
        '''
        subprocess.run(['osascript', '-e', applescript], capture_output=True)
    
    def open_chrome(self):
        """Otevře Chrome s aplikací"""
        if self.find_chrome_tab():
            print("✅ Používám existující Chrome tab")
            self.refresh_chrome()
        else:
            print("🌐 Otevírám Chrome...")
            subprocess.run(['open', '-a', 'Google Chrome', self.url])
    
    def kill_dotnet_processes(self):
        """Ukončí všechny dotnet procesy OptimalyAI"""
        print("🔪 Ukončuji existující dotnet procesy OptimalyAI...")
        
        # Nejdřív graceful termination
        subprocess.run("pkill -f 'dotnet.*OptimalyAI'", shell=True, capture_output=True)
        time.sleep(2)
        
        # Force kill pokud ještě běží
        check_result = subprocess.run("pgrep -f 'dotnet.*OptimalyAI'", shell=True, capture_output=True, text=True)
        if check_result.stdout:
            subprocess.run("pkill -9 -f 'dotnet.*OptimalyAI'", shell=True, capture_output=True)
            time.sleep(1)
        
        # Ukončíme i procesy na portu
        if self.is_port_in_use():
            result = subprocess.run(['lsof', '-ti', f':{self.port}'], capture_output=True, text=True)
            if result.stdout:
                pids = result.stdout.strip().split('\n')
                for pid in pids:
                    subprocess.run(['kill', '-9', pid])
            time.sleep(1)
            
        print("✅ Všechny procesy ukončeny")
    
    def run_detached(self):
        """Spustí aplikaci v pozadí (detached)"""
        self.kill_dotnet_processes()
        
        print("🚀 Spouštím OptimalyAI aplikaci v pozadí...")
        
        try:
            # Spustíme dotnet run v pozadí s nohup
            with open(self.log_file, 'w') as log:
                self.process = subprocess.Popen([
                    'nohup',
                    'dotnet', 'run', 
                    '--project', 'OptimalyAI.csproj',
                    '--urls', f'https://localhost:{self.port}'
                ], 
                cwd=os.getcwd(),
                stdout=log,
                stderr=subprocess.STDOUT,
                preexec_fn=os.setpgrp,  # Detach from parent process group
                start_new_session=True)
            
            print(f"📝 Logy se ukládají do: {self.log_file}")
            
            # Počkáme až aplikace naběhne
            print("⏳ Čekám na spuštění aplikace...")
            for i in range(30):
                if self.is_port_in_use():
                    print("✅ Aplikace běží!")
                    time.sleep(2)
                    self.open_chrome()
                    break
                time.sleep(1)
                sys.stdout.write('.')
                sys.stdout.flush()
            else:
                print("\n⚠️ Aplikace se nespustila během 30 sekund")
                print("🔍 Zkontroluj logy:")
                subprocess.run(['tail', '-20', self.log_file])
                
        except Exception as e:
            print(f"❌ Chyba: {e}")
    
    def show_status(self):
        """Zobrazí status aplikace"""
        if self.is_port_in_use():
            print(f"✅ Aplikace běží na: {self.url}")
            
            # Najdeme PID procesu
            result = subprocess.run("pgrep -f 'dotnet.*OptimalyAI'", shell=True, capture_output=True, text=True)
            if result.stdout:
                pids = result.stdout.strip().split('\n')
                print(f"📊 Běžící procesy: {', '.join(pids)}")
        else:
            print("❌ Aplikace neběží")
    
    def show_logs(self, lines=50):
        """Zobrazí posledních N řádků logů"""
        # Najdeme nejnovější log soubor
        result = subprocess.run("ls -t dev-runner-*.log 2>/dev/null | head -1", 
                               shell=True, capture_output=True, text=True)
        if result.stdout:
            log_file = result.stdout.strip()
            print(f"📝 Zobrazuji posledních {lines} řádků z {log_file}:")
            subprocess.run(['tail', f'-{lines}', log_file])
        else:
            print("❌ Žádné logy nenalezeny")

def print_help():
    """Zobrazí nápovědu"""
    print("""
🚀 OptimalyAI Dev Runner - Příkazy:
    
    run-dev.py          - Restartuje a spustí aplikaci v pozadí
    run-dev.py status   - Zobrazí status aplikace
    run-dev.py logs     - Zobrazí posledních 50 řádků logů
    run-dev.py logs N   - Zobrazí posledních N řádků logů
    run-dev.py stop     - Zastaví aplikaci
    run-dev.py restart  - Restartuje aplikaci
    run-dev.py help     - Zobrazí tuto nápovědu
    """)

def main():
    runner = DevRunner()
    
    # Parsování argumentů
    if len(sys.argv) > 1:
        command = sys.argv[1].lower()
        
        if command == 'status':
            runner.show_status()
        elif command == 'logs':
            lines = int(sys.argv[2]) if len(sys.argv) > 2 else 50
            runner.show_logs(lines)
        elif command == 'stop':
            print("🛑 Zastavuji aplikaci...")
            runner.kill_dotnet_processes()
            print("✅ Aplikace zastavena")
        elif command == 'restart':
            print("🔄 Restartuji aplikaci...")
            runner.run_detached()
        elif command == 'help':
            print_help()
        else:
            print(f"❌ Neznámý příkaz: {command}")
            print_help()
    else:
        # Výchozí akce - restart a spuštění
        print("╔════════════════════════════════════════╗")
        print("║   OptimalyAI Development Runner        ║")
        print("║   Auto-restart & Background Mode       ║")
        print("╚════════════════════════════════════════╝\n")
        
        runner.run_detached()
        
        print(f"\n🌐 Aplikace běží na: {runner.url}")
        print("📝 Pro zobrazení logů: ./run-dev.py logs")
        print("🛑 Pro zastavení: ./run-dev.py stop")
        print("🔄 Pro restart: ./run-dev.py restart")

if __name__ == "__main__":
    main()