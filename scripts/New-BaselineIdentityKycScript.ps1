param()
$ErrorActionPreference = 'Stop'
$initial = Get-Content (Join-Path $PSScriptRoot 'InitialIdentityKyc.sql') -Raw
$validation = Get-Content (Join-Path $PSScriptRoot 'BaselineIdentityKyc.validation.sql') -Raw
$ddlStart = $initial.IndexOf('CREATE TABLE public."RegistrationApplications"')
$historyStart = $initial.IndexOf('INSERT INTO public."__EFMigrationsHistory"')
if ($ddlStart -lt 0 -or $historyStart -lt 0) { throw 'Unexpected initial migration script format.' }
$reference = $initial.Substring($ddlStart, $historyStart - $ddlStart).Replace('public.', '_identity_kyc_reference.')
$history = $initial.Substring($historyStart)
$prefix = @'
-- One-time adoption of the existing public schema. Does not recreate user tables.
-- Validates schema, normalizes constraint/index names and installs trigger functions.
-- Any mismatch aborts the transaction. Run the ENTIRE script on the intended database.
BEGIN;
SET LOCAL lock_timeout = '5s';
SELECT pg_advisory_xact_lock(184732910);
CREATE TABLE IF NOT EXISTS public."__EFMigrationsHistory" (
    "MigrationId" VARCHAR(150) PRIMARY KEY,
    "ProductVersion" VARCHAR(32) NOT NULL
);
DO $guard$
BEGIN
    IF EXISTS (SELECT 1 FROM public."__EFMigrationsHistory") THEN
        RAISE EXCEPTION 'Migration history is not empty; baseline is a one-time operation';
    END IF;
END;
$guard$;
-- CREATE without IF NOT EXISTS ensures an existing schema is never removed.
CREATE SCHEMA _identity_kyc_reference;
'@
$suffix = @'
-- Only the scratch schema created above is removed; public tables remain.
DROP SCHEMA _identity_kyc_reference CASCADE;
'@
$outputPath = Join-Path $PSScriptRoot 'BaselineExistingIdentityKyc.sql'
Set-Content -LiteralPath $outputPath -Value ($prefix + "`n" + $reference + "`n" + $validation + "`n" + $suffix + "`n" + $history) -Encoding utf8
Write-Host "Generated $outputPath"
