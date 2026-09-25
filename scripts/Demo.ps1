param([string]$BaseUrl = 'http://localhost:5080')
$ErrorActionPreference = 'Stop'
$BaseUrl = $BaseUrl.TrimEnd('/')
Invoke-RestMethod "$BaseUrl/health" | Format-List
$body = @{
    customerId = 'demo-customer'
    items = @(
        @{ productId = 'book'; quantity = 2; unitPrice = 12.50 },
        @{ productId = 'pen'; quantity = 3; unitPrice = 0.10 }
    )
} | ConvertTo-Json -Depth 5
$order = Invoke-RestMethod "$BaseUrl/orders" -Method Post -ContentType 'application/json' -Body $body
Write-Host "Created multi-item order $($order.id), total $($order.total)"
Invoke-RestMethod "$BaseUrl/orders/$($order.id)" | ConvertTo-Json -Depth 5
foreach ($status in @('PROCESSING', 'SHIPPED', 'DELIVERED')) {
    $updated = Invoke-RestMethod "$BaseUrl/orders/$($order.id)/status" -Method Patch -ContentType 'application/json' -Body (@{status=$status} | ConvertTo-Json)
    Write-Host "Status: $($updated.status)"
}
try {
    Invoke-RestMethod "$BaseUrl/orders/$($order.id)/cancel" -Method Post | Out-Null
    throw 'Unexpected success: a delivered order must not be cancellable.'
}
catch {
    if ($null -eq $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 409) { throw }
    Write-Host 'Correctly rejected cancellation of delivered order: HTTP 409'
}
$cancellable = Invoke-RestMethod "$BaseUrl/orders" -Method Post -ContentType 'application/json' -Body $body
$cancelled = Invoke-RestMethod "$BaseUrl/orders/$($cancellable.id)/cancel" -Method Post
Write-Host "Second order cancelled: $($cancelled.status)"
$pending = Invoke-RestMethod "$BaseUrl/orders" -Method Post -ContentType 'application/json' -Body $body
Write-Host "Third order left for the background worker: $($pending.id)"
Invoke-RestMethod "$BaseUrl/orders?status=PENDING" | ConvertTo-Json -Depth 6
Write-Host "After the next worker tick, inspect: $BaseUrl/orders/$($pending.id)"
