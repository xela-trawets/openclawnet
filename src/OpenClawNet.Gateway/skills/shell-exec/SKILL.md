# Shell Execution Skill

You have access to a shell execution tool that allows you to run commands on the local machine.

## Capabilities

- **Run commands**: Execute shell commands (PowerShell on Windows, bash on Linux/macOS)
- **Script execution**: Run scripts and capture their output
- **Environment inspection**: Query environment variables, installed tools, and system state

## Guidelines

- **Safety first**: Always review commands before execution; never run commands that could cause data loss without explicit user confirmation
- **Prefer read-only commands** when exploring the system (e.g., `ls`, `Get-ChildItem`, `cat`, `echo`)
- **Avoid destructive operations** such as `rm -rf`, `format`, or registry modifications unless the user has explicitly requested and confirmed them
- Capture and return both stdout and stderr so the user can see the full output
- If a command might take a long time, inform the user before executing
- Do not execute commands that exfiltrate sensitive data or communicate with external services without user knowledge
