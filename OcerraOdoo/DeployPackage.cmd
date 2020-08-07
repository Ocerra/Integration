@echo off

echo "Press enter to continue..."

pause

For /f "tokens=2-4 delims=/ " %%a in ('date /t') do (set mydate=%%c.%%a%%b)
For /f "tokens=1-2 delims=/:" %%a in ("%TIME%") do (set mytime=%%a%%b)
set tm=%mydate%.%mytime%
set pkg=packages/OcerraOdoo.%tm%.zip

"C:\Program Files\7-Zip\7z.exe" a -tzip "%pkg%" "C:\Projects\OcerraIntegration\Integration\OcerraOdoo\bin\Web\*" -x!Web.config -x!Settings.json

curl -X POST https://ocerra.octopus.app/api/packages/raw -H "X-Octopus-ApiKey: API-ZMQL4T1NTJXL35A4YCGPIHNMKG4" -F "data=@%pkg%"
