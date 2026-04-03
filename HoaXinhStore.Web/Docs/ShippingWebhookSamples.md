# Mau webhook van chuyen (GHN/GHTK/Ahamove)

Endpoint he thong:

- `POST /api/shipping/webhook`
- Header bat buoc: `X-Shipping-Key: <gia-tri-ShippingIntegration:WebhookKey>`
- Content-Type: `application/json`

Body chuan:

```json
{
  "orderNo": "HX2026040315304591",
  "carrier": "GHN",
  "trackingCode": "S123456789VN",
  "status": "shipping",
  "note": "Don hang dang tren duong giao"
}
```

Map trang thai:

- `pending` -> `Confirmed`
- `picked` -> `Preparing`
- `shipping` / `in_transit` -> `Shipping`
- `delivered` -> `Completed`
- `failed` -> `DeliveryFailed`
- `cancelled` -> `Cancelled`
- `returned` -> `Returned`

## 1) Mau cho GHN

Payload gui vao he thong:

```json
{
  "orderNo": "HX2026040315304591",
  "carrier": "GHN",
  "trackingCode": "GHN123456789",
  "status": "in_transit",
  "note": "GHN cap nhat: dang giao den nguoi nhan"
}
```

## 2) Mau cho GHTK

Payload gui vao he thong:

```json
{
  "orderNo": "HX2026040315304591",
  "carrier": "GHTK",
  "trackingCode": "S123ABC456",
  "status": "delivered",
  "note": "GHTK cap nhat: giao thanh cong"
}
```

## 3) Mau cho Ahamove

Payload gui vao he thong:

```json
{
  "orderNo": "HX2026040315304591",
  "carrier": "AHAMOVE",
  "trackingCode": "AHM99887766",
  "status": "failed",
  "note": "Khach hen giao lai"
}
```

## Test nhanh bang curl

```bash
curl -X POST "http://localhost:7399/api/shipping/webhook" \
  -H "Content-Type: application/json" \
  -H "X-Shipping-Key: change-this-shipping-webhook-key" \
  -d "{\"orderNo\":\"HX2026040315304591\",\"carrier\":\"GHN\",\"trackingCode\":\"GHN123456789\",\"status\":\"shipping\",\"note\":\"Dang giao\"}"
```

