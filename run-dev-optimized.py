#!/usr/bin/env python3
"""
Optimized Development Runner for OptimalyAI
Implements incremental builds, hot reload, and build performance optimizations
"""
import subprocess
import sys
import os
import time
import signal
import datetime
from pathlib import Path

class DevelopmentRunner:
    def __init__(self):
        self.project_root = Path(__file__).parent.absolute()
        self.log_dir = self.project_root / "logs"
        self.log_dir.mkdir(exist_ok=True)
        self.current_process = None
        
    def log(self, message):
        """Print timestamped log message"""
        timestamp = datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        print(f"[{timestamp}] {message}")
        
    def ensure_environment(self):
        """Ensure development environment is set"""
        os.environ["ASPNETCORE_ENVIRONMENT"] = "Development"
        os.environ["DOTNET_WATCH_SUPPRESS_EMOJIS"] = "1"
        
        # Optimization environment variables
        os.environ["DOTNET_USE_POLLING_FILE_WATCHER"] = "false"
        os.environ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
        os.environ["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1"
        os.environ["DOTNET_NOLOGO"] = "1"
        
        # Enable MSBuild optimizations
        os.environ["MSBUILDNODECOUNT"] = str(os.cpu_count() or 4)
        os.environ["MSBUILDUSECACHE"] = "1"
        
    def clean_build_artifacts(self):
        """Clean build artifacts for faster rebuilds"""
        self.log("Cleaning build artifacts...")
        
        # Directories to clean
        clean_dirs = [
            "bin", "obj", 
            "OAI.Core/bin", "OAI.Core/obj",
            "OAI.ServiceLayer/bin", "OAI.ServiceLayer/obj", 
            "OAI.DataLayer/bin", "OAI.DataLayer/obj"
        ]
        
        for dir_name in clean_dirs:
            dir_path = self.project_root / dir_name
            if dir_path.exists():
                self.log(f"  Removing {dir_path}")
                subprocess.run(["rm", "-rf", str(dir_path)], check=False)
                
        self.log("Clean completed")
        
    def build_only(self):
        """Build the project without running"""
        self.log("Building project...")
        
        cmd = [
            "dotnet", "build",
            "--configuration", "Debug",
            "--no-incremental", "false",  # Enable incremental builds
            "--no-restore", "false",
            "-p:BuildInParallel=true",
            "-p:EnableDefaultCompileItems=false",
            "-p:GenerateDocumentationFile=false",
            "-p:DebugType=embedded",
            "-p:PublishTrimmed=false",
            "-p:PublishReadyToRun=false",
            "-maxcpucount",
            "-v", "minimal"
        ]
        
        result = subprocess.run(cmd, cwd=self.project_root)
        return result.returncode == 0
        
    def run_watch_mode(self):
        """Run with dotnet watch for hot reload"""
        self.log("Starting in watch mode (hot reload enabled)...")
        self.log("Press Ctrl+C to stop")
        self.log("")
        self.log("Hot reload is enabled for:")
        self.log("  - C# code changes")
        self.log("  - Razor views")
        self.log("  - Static files (CSS, JS)")
        self.log("")
        
        cmd = [
            "dotnet", "watch", "run",
            "--project", "OptimalyAI.csproj",
            "--no-hot-reload", "false",
            "--non-interactive"
        ]
        
        try:
            self.current_process = subprocess.Popen(
                cmd,
                cwd=self.project_root,
                stdout=sys.stdout,
                stderr=sys.stderr
            )
            self.current_process.wait()
        except KeyboardInterrupt:
            self.log("\nShutting down gracefully...")
            if self.current_process:
                self.current_process.terminate()
                self.current_process.wait(timeout=5)
                
    def run_standard_mode(self):
        """Run without watch mode"""
        self.log("Starting in standard mode...")
        
        cmd = [
            "dotnet", "run",
            "--project", "OptimalyAI.csproj",
            "--configuration", "Debug",
            "--no-build", "false",
            "--no-restore", "false"
        ]
        
        try:
            self.current_process = subprocess.Popen(
                cmd,
                cwd=self.project_root,
                stdout=sys.stdout,
                stderr=sys.stderr
            )
            self.current_process.wait()
        except KeyboardInterrupt:
            self.log("\nShutting down...")
            if self.current_process:
                self.current_process.terminate()
                self.current_process.wait(timeout=5)
                
    def measure_build_time(self):
        """Measure and report build time"""
        start_time = time.time()
        success = self.build_only()
        elapsed = time.time() - start_time
        
        if success:
            self.log(f"Build completed in {elapsed:.2f} seconds")
        else:
            self.log(f"Build failed after {elapsed:.2f} seconds")
            
        return success
        
    def print_usage(self):
        """Print usage information"""
        print("""
OptimalyAI Optimized Development Runner

Usage:
  ./run-dev-optimized.py          # Run with hot reload (recommended)
  ./run-dev-optimized.py watch    # Same as above
  ./run-dev-optimized.py build    # Build only
  ./run-dev-optimized.py clean    # Clean build artifacts
  ./run-dev-optimized.py standard # Run without watch mode
  ./run-dev-optimized.py time     # Measure build time
  ./run-dev-optimized.py help     # Show this help

Environment variables:
  MSBUILDNODECOUNT     # Number of parallel build nodes (default: CPU count)
  DOTNET_WATCH_RESTART # Force restart on file changes (default: auto)
""")

    def run(self, args):
        """Main entry point"""
        self.ensure_environment()
        
        command = args[0] if args else "watch"
        
        if command in ["help", "-h", "--help"]:
            self.print_usage()
        elif command == "clean":
            self.clean_build_artifacts()
        elif command == "build":
            self.build_only()
        elif command == "time":
            self.measure_build_time()
        elif command == "standard":
            self.run_standard_mode()
        elif command in ["watch", "run"]:
            self.run_watch_mode()
        else:
            self.log(f"Unknown command: {command}")
            self.print_usage()
            sys.exit(1)

def main():
    """Main entry point"""
    runner = DevelopmentRunner()
    
    # Handle Ctrl+C gracefully
    def signal_handler(sig, frame):
        print("\nShutdown signal received...")
        sys.exit(0)
        
    signal.signal(signal.SIGINT, signal_handler)
    
    try:
        runner.run(sys.argv[1:])
    except Exception as e:
        print(f"Error: {e}")
        sys.exit(1)

if __name__ == "__main__":
    main()