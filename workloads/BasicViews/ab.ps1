param([int] $iterations = 3000)

$url = "http://127.0.0.1:5000/?name=Joey`&age=15`&birthdate=9-9-1985"

Write-Host (curl $url)

& ab -k -n $iterations -c 8 $url