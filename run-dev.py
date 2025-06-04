#!/usr/bin/env python3
"""
OptimalyAI Development Runner
- Spouští dotnet watch přímo
- Inteligentní správa Chrome tabů
- Hot reload s automatickým refreshem
"""

import subprocess
import sys
import os
import time
import socket
import signal

class DevRunner:
    def __init__(self):
        self.port = 5005
        self.url = f"https://localhost:{self.port}"
        self.process = None
        
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
    
    def kill_process(self):
        """Ukončí běžící dotnet proces"""
        if self.process:
            print("🛑 Ukončuji aplikaci...")
            self.process.terminate()
            try:
                self.process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                self.process.kill()
            print("✅ Aplikace ukončena")
    
    def setup_signal_handlers(self):
        """Nastaví signal handlery pro ukončení"""
        def signal_handler(sig, frame):
            print("\n🛑 Ukončuji...")
            self.kill_process()
            sys.exit(0)
        
        signal.signal(signal.SIGINT, signal_handler)
        signal.signal(signal.SIGTERM, signal_handler)
    
    def run(self):
        """Hlavní run metoda"""
        self.setup_signal_handlers()
        
        print("🚀 Spouštím OptimalyAI aplikaci...")
        
        try:
            # Spustíme dotnet watch přímo s explicitním URL
            self.process = subprocess.Popen([
                'dotnet', 'watch', 'run', 
                '--project', 'OptimalyAI.csproj',
                '--urls', f'https://localhost:{self.port}'
            ], cwd=os.getcwd())
            
            # Počkáme až aplikace naběhne
            print("⏳ Čekám na spuštění aplikace...")
            for i in range(30):
                if self.is_port_in_use():
                    print("✅ Aplikace běží!")
                    time.sleep(2)
                    self.open_chrome()
                    break
                time.sleep(1)
            else:
                print("⚠️ Aplikace se nespustila během 30 sekund")
            
            print(f"\n🌐 Aplikace běží na: {self.url}")
            print("📝 Pro ukončení stiskni Ctrl+C")
            print("=" * 50)
            
            # Čekáme na ukončení procesu
            self.process.wait()
            
        except KeyboardInterrupt:
            print("\n🛑 Ukončuji...")
            self.kill_process()
        except Exception as e:
            print(f"❌ Chyba: {e}")
            self.kill_process()

def main():
    print("╔════════════════════════════════════════╗")
    print("║   OptimalyAI Development Runner        ║")
    print("║   Direct dotnet watch                  ║")
    print("╚════════════════════════════════════════╝\n")
    
    runner = DevRunner()
    
    # Kontrola jestli port není obsazený
    if runner.is_port_in_use():
        print(f"⚠️ Port {runner.port} je už obsazený!")
        print("   Buď už aplikace běží, nebo běží jiná aplikace na tomto portu.")
        print()
        print("🔧 Co chceš udělat?")
        print("   [k] Ukončit procesy na portu a restartovat")
        print("   [o] Otevřít Chrome na běžící aplikaci")
        print("   [q] Quit")
        
        choice = input("\nVolba: ").lower()
        
        if choice == 'k':
            print("🛑 Ukončuji procesy na portu...")
            # Najdeme a ukončíme process na portu
            result = subprocess.run(['lsof', '-ti', f':{runner.port}'], capture_output=True, text=True)
            if result.stdout:
                pids = result.stdout.strip().split('\n')
                for pid in pids:
                    subprocess.run(['kill', '-9', pid])
                    print(f"   Ukončen proces PID: {pid}")
            
            subprocess.run(['pkill', '-f', 'dotnet.*watch.*run'], capture_output=True)
            subprocess.run(['pkill', '-f', 'OptimalyAI'], capture_output=True)
            time.sleep(2)
            if not runner.is_port_in_use():
                print("✅ Port uvolněn, spouštím aplikaci...")
                runner.run()
            else:
                print("❌ Port stále obsazený, zkus manuálně ukončit procesy")
        elif choice == 'o':
            print("🌐 Otevírám Chrome...")
            runner.open_chrome()
        else:
            print("👋 Končím")
        return
    
    runner.run()

if __name__ == "__main__":
    main()