# API Conventions

## Versioning
All endpoints are versioned in the route: `/api/v1/...`

## Response Envelope
Standard response format:
```json
{
  "success": true,
  "message": "Optional message",
  "data": { ... },
  "errors": [],
  "correlationId": "uuid"
}
```

## Error Handling
Central exception handling middleware maps internal errors to the response envelope with 500 status code (or specific codes where applicable).
