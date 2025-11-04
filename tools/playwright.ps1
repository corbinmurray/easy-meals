<#
.SYNOPSIS
    Installs Playwright browsers for the Recipe Engine.

.DESCRIPTION
    This script installs Chromium browser binaries required by Playwright for dynamic recipe discovery.
    Only Chromium is installed to minimize storage (Playwright can install 3 browsers totaling ~1GB).

.PARAMETER BrowserName
    The browser to install. Default is 'chromium'. Other options: 'firefox', 'webkit', 'chrome'.

.EXAMPLE
    .\playwright.ps1 install chromium
    Installs Chromium browser for Playwright.

.EXAMPLE
    .\playwright.ps1 install
    Installs Chromium browser (default).

.NOTES
    Playwright browsers are installed to:
    - Windows: %USERPROFILE%\.cache\ms-playwright
    - Linux: ~/.cache/ms-playwright
    
    For Docker deployments, use the official Playwright image which has browsers pre-installed:
    mcr.microsoft.com/playwright/dotnet:v1.40.0-focal
#>

param(
    [Parameter(Mandatory = $false, Position = 0)]
    [string]$Action = "install",
    
    [Parameter(Mandatory = $false, Position = 1)]
    [string]$BrowserName = "chromium"
)

$ErrorActionPreference = "Stop"

function Write-Info {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "[SUCCESS] $Message" -ForegroundColor Green
}

function Write-Fail {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}

function Install-PlaywrightBrowsers {
    param([string]$Browser)
    
    Write-Info "Installing Playwright browser: $Browser"
    Write-Info "This may take several minutes depending on your connection speed..."
    
    # Check if pwsh is available (PowerShell Core)
    $pwshPath = Get-Command pwsh -ErrorAction SilentlyContinue
    if ($null -eq $pwshPath) {
        Write-Fail "PowerShell Core (pwsh) is required but not found."
        Write-Info "Install from: https://github.com/PowerShell/PowerShell/releases"
        exit 1
    }
    
    # Check if Playwright CLI is available via npx
    Write-Info "Checking for Playwright CLI..."
    try {
        $playwrightVersion = npx --yes playwright --version 2>&1
        Write-Info "Playwright CLI version: $playwrightVersion"
    }
    catch {
        Write-Fail "Playwright CLI not found. Install Node.js and npm first."
        Write-Info "Install from: https://nodejs.org/"
        exit 1
    }
    
    # Install browser
    Write-Info "Installing $Browser browser binaries..."
    try {
        npx --yes playwright install $Browser
        Write-Success "Playwright browser '$Browser' installed successfully!"
        Write-Info "Browser location: $env:USERPROFILE\.cache\ms-playwright"
    }
    catch {
        Write-Fail "Failed to install Playwright browser: $_"
        exit 1
    }
}

function Show-Usage {
    Write-Host @"
Usage: playwright.ps1 [action] [browser]

Actions:
    install [browser]   Install Playwright browser (default: chromium)

Browsers:
    chromium            Chromium browser (default, recommended for Recipe Engine)
    firefox             Firefox browser
    webkit              WebKit browser (Safari engine)
    chrome              Google Chrome (stable channel)

Examples:
    .\playwright.ps1 install
    .\playwright.ps1 install chromium
    .\playwright.ps1 install firefox

Notes:
    - Chromium is the recommended browser for the Recipe Engine (lightweight, fast)
    - Browsers are installed to: %USERPROFILE%\.cache\ms-playwright
    - For Docker deployments, use mcr.microsoft.com/playwright/dotnet:v1.40.0-focal
"@
}

# Main execution
switch ($Action.ToLower()) {
    "install" {
        Install-PlaywrightBrowsers -Browser $BrowserName
    }
    "help" {
        Show-Usage
    }
    default {
        Write-Fail "Unknown action: $Action"
        Show-Usage
        exit 1
    }
}
