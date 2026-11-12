<#
.SYNOPSIS
    Tries a complete sign-in (Authorization Code + PKCE) against the local Identity service.

.DESCRIPTION
    Plays the public client of a seeded tenant: creates the PKCE verifier and challenge,
    starts the sign-in, exchanges the code for tokens and calls userinfo.

    By default it opens the sign-in page in your browser and asks you to paste the address
    you land on (http://localhost:5173/callback?code=...). That page shows an error because
    no frontend exists yet; only its address matters.

    With -Automatic it submits the login form itself, using the seeded password from the
    repository's .env file, so the whole flow runs without a browser.

    Requires the stack to be running (./dev.ps1 up). Works on Windows PowerShell 5.1 and PowerShell 7.

.EXAMPLE
    ./services/identity/scripts/Try-SignIn.ps1
.EXAMPLE
    ./services/identity/scripts/Try-SignIn.ps1 -Tenant B -Automatic
#>
[CmdletBinding()]
param(
    # Seeded tenant to sign in to.
    [ValidateSet('A', 'B')]
    [string]$Tenant = 'A',

    # Requested scopes. Without 'profile', the username is not released.
    [string]$Scope = 'openid profile',

    # Submits the login form without a browser, with the password from .env.
    [switch]$Automatic,

    # Also prints the raw access and ID tokens.
    [switch]$ShowTokens,

    [string]$BaseUrl = 'http://localhost:5001'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http

$clientId = "tenant-$($Tenant.ToLowerInvariant())-web"
$redirectUri = 'http://localhost:5173/callback'
$username = 'demo.user'

function ConvertTo-Base64Url([byte[]]$Bytes) {
    [Convert]::ToBase64String($Bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

function ConvertFrom-Jwt([string]$Jwt) {
    $payload = $Jwt.Split('.')[1].Replace('-', '+').Replace('_', '/')
    $payload += '=' * ((4 - $payload.Length % 4) % 4)
    [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($payload)) | ConvertFrom-Json
}

function Get-QueryValue([string]$Url, [string]$Name) {
    $query = ([Uri]$Url).Query.TrimStart('?')
    foreach ($pair in $query.Split('&')) {
        $parts = $pair.Split('=', 2)
        if ($parts[0] -eq $Name -and $parts.Count -eq 2) { return [Uri]::UnescapeDataString($parts[1]) }
    }
    return $null
}

function Get-HiddenField([string]$Html, [string]$Name) {
    $match = [regex]::Match($Html, "name=""$Name""[^>]*value=""([^""]*)""")
    if (-not $match.Success) { throw "The login form has no field '$Name'." }
    [Net.WebUtility]::HtmlDecode($match.Groups[1].Value)
}

function Get-SeedPassword {
    $envFile = Join-Path $PSScriptRoot '../../../.env'
    $key = "SEED_TENANT_$($Tenant)_USER_PASSWORD"
    if (Test-Path $envFile) {
        $line = Get-Content $envFile | Where-Object { $_ -match "^$key=" } | Select-Object -First 1
        if ($line) { return $line.Substring($key.Length + 1) }
    }
    $secure = Read-Host "Password for $username in tenant $Tenant ($key)" -AsSecureString
    [Net.NetworkCredential]::new('', $secure).Password
}

function Invoke-AutomaticLogin([Net.Http.HttpClient]$Http, [string]$AuthorizeUrl) {
    $start = $Http.GetAsync($AuthorizeUrl).Result
    $location = $start.Headers.Location
    if ($null -eq $location) { throw "Sign-in did not start: HTTP $([int]$start.StatusCode). $($start.Content.ReadAsStringAsync().Result)" }

    $form = $Http.GetStringAsync([Uri]::new([Uri]$BaseUrl, $location)).Result
    $fields = New-Object 'System.Collections.Generic.Dictionary[string,string]'
    $fields['username'] = $username
    $fields['password'] = Get-SeedPassword
    $fields['returnUrl'] = Get-HiddenField $form 'returnUrl'
    $fields['__RequestVerificationToken'] = Get-HiddenField $form '__RequestVerificationToken'

    $login = $Http.PostAsync("$BaseUrl/account/login", [Net.Http.FormUrlEncodedContent]::new($fields)).Result
    if ([int]$login.StatusCode -ne 302) { throw "The credentials were rejected (HTTP $([int]$login.StatusCode))." }

    $authorize = $Http.GetAsync([Uri]::new([Uri]$BaseUrl, $login.Headers.Location)).Result
    if ($null -eq $authorize.Headers.Location) { throw "No redirect to the client: HTTP $([int]$authorize.StatusCode)." }
    $authorize.Headers.Location.ToString()
}

# 1. PKCE: a random verifier, and its SHA-256 as the challenge.
$bytes = New-Object byte[] 32
[Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
$verifier = ConvertTo-Base64Url $bytes
$challenge = ConvertTo-Base64Url ([Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::ASCII.GetBytes($verifier)))
$state = ConvertTo-Base64Url ((1..12) | ForEach-Object { [byte](Get-Random -Maximum 256) })

$authorizeUrl = "$BaseUrl/connect/authorize?client_id=$clientId&response_type=code" +
    "&scope=$([Uri]::EscapeDataString($Scope))&redirect_uri=$([Uri]::EscapeDataString($redirectUri))" +
    "&state=$state&code_challenge=$challenge&code_challenge_method=S256"

$handler = New-Object Net.Http.HttpClientHandler
$handler.AllowAutoRedirect = $false
$handler.CookieContainer = New-Object Net.CookieContainer
$http = New-Object Net.Http.HttpClient($handler)

try {
    $health = $http.GetAsync("$BaseUrl/health/ready").Result
}
catch {
    $health = $null
}
if ($null -eq $health -or [int]$health.StatusCode -eq 0) { throw "Identity is not reachable at $BaseUrl. Start it with ./dev.ps1 up." }
if (-not $health.IsSuccessStatusCode) { throw "Identity is running but not ready (HTTP $([int]$health.StatusCode)). Check ./dev.ps1 ps." }

Write-Host "Tenant $Tenant, client $clientId, scope '$Scope'" -ForegroundColor Cyan

# 2. Sign in and get the code.
if ($Automatic) {
    $callback = Invoke-AutomaticLogin $http $authorizeUrl
}
else {
    Write-Host "Opening the sign-in page. Sign in as '$username' with SEED_TENANT_$($Tenant)_USER_PASSWORD from .env."
    Write-Host "You will land on ${redirectUri}?code=... (an error page is expected). Copy that address."
    Start-Process $authorizeUrl
    $callback = Read-Host 'Paste the address here'
}

$error_ = Get-QueryValue $callback 'error'
if ($error_) { throw "The sign-in was rejected: $error_ $(Get-QueryValue $callback 'error_description')" }
if ((Get-QueryValue $callback 'state') -ne $state) { throw 'The state does not match this sign-in; use the address from this run.' }
$code = Get-QueryValue $callback 'code'
if (-not $code) { throw 'The address has no code.' }
Write-Host 'Code received.' -ForegroundColor Green

# 3. Exchange the code, proving possession with the verifier.
$tokenFields = New-Object 'System.Collections.Generic.Dictionary[string,string]'
$tokenFields['grant_type'] = 'authorization_code'
$tokenFields['client_id'] = $clientId
$tokenFields['code'] = $code
$tokenFields['redirect_uri'] = $redirectUri
$tokenFields['code_verifier'] = $verifier
$exchange = $http.PostAsync("$BaseUrl/connect/token", [Net.Http.FormUrlEncodedContent]::new($tokenFields)).Result
$body = $exchange.Content.ReadAsStringAsync().Result
if (-not $exchange.IsSuccessStatusCode) { throw "The code exchange failed (HTTP $([int]$exchange.StatusCode)): $body" }
$tokens = $body | ConvertFrom-Json
Write-Host "Tokens received; the access token expires in $($tokens.expires_in) seconds." -ForegroundColor Green

# 4. Ask for the user's identity.
$request = New-Object Net.Http.HttpRequestMessage([Net.Http.HttpMethod]::Get, "$BaseUrl/connect/userinfo")
$request.Headers.Authorization = New-Object Net.Http.Headers.AuthenticationHeaderValue('Bearer', $tokens.access_token)
$userInfo = $http.SendAsync($request).Result.Content.ReadAsStringAsync().Result | ConvertFrom-Json

$claims = ConvertFrom-Jwt $tokens.access_token
$shown = 'iss', 'aud', 'sub', 'tenant_id', 'preferred_username', 'client_id' |
    Where-Object { $claims.PSObject.Properties.Name -contains $_ }

Write-Host "`nUserinfo" -ForegroundColor Cyan
Write-Host ($userInfo | Format-List | Out-String).Trim()
Write-Host "`nAccess token claims" -ForegroundColor Cyan
Write-Host ($claims | Select-Object ($shown + @{ Name = 'expires'; Expression = { [DateTimeOffset]::FromUnixTimeSeconds($_.exp).LocalDateTime } }) |
    Format-List | Out-String).Trim()
if ($claims.PSObject.Properties.Name -notcontains 'preferred_username') {
    Write-Host "(no preferred_username: the 'profile' scope was not requested)" -ForegroundColor DarkGray
}

if ($ShowTokens) {
    Write-Host "access_token: $($tokens.access_token)"
    Write-Host "id_token:     $($tokens.id_token)"
}

$http.Dispose()
