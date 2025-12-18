# API Key Authentication System

## Overview

AI Manager Core ใช้ระบบ API Key สำหรับ authentication ทุก request ที่เข้ามาจะต้องมี API Key ที่ถูกต้อง ยกเว้น endpoints ที่ได้รับการยกเว้น

## Quick Start

### 1. Initial Setup - Get Master Key

เมื่อรัน AI Manager API ครั้งแรก ระบบจะสร้าง Master API Key อัตโนมัติและแสดงใน console log:

```
========================================
INITIAL SETUP: Master API Key Generated
Key: aim_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
SAVE THIS KEY! It won't be shown again!
========================================
```

**สำคัญ:** บันทึก Master Key นี้ไว้ เพราะจะไม่แสดงอีก!

### 2. Using API Key

#### Option A: Header (แนะนำ)

```bash
curl -H "X-API-Key: aim_your_api_key_here" \
     http://localhost:5000/api/status/health
```

#### Option B: Authorization Header

```bash
# Bearer scheme
curl -H "Authorization: Bearer aim_your_api_key_here" \
     http://localhost:5000/api/status/health

# ApiKey scheme
curl -H "Authorization: ApiKey aim_your_api_key_here" \
     http://localhost:5000/api/status/health
```

#### Option C: Query Parameter

```bash
curl "http://localhost:5000/api/status/health?api_key=aim_your_api_key_here"
```

## API Endpoints

### Manage API Keys

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/apikeys` | List all API keys |
| GET | `/api/apikeys/{id}` | Get key by ID |
| POST | `/api/apikeys` | Create new API key |
| PUT | `/api/apikeys/{id}` | Update API key |
| DELETE | `/api/apikeys/{id}` | Revoke/Delete API key |
| POST | `/api/apikeys/{id}/regenerate` | Regenerate key |
| POST | `/api/apikeys/{id}/toggle` | Enable/Disable key |
| GET | `/api/apikeys/scopes` | List available scopes |
| GET | `/api/apikeys/validate` | Validate current key |
| POST | `/api/apikeys/setup` | Generate master key (first run only) |

### Create API Key

```bash
curl -X POST "http://localhost:5000/api/apikeys" \
     -H "X-API-Key: aim_master_key" \
     -H "Content-Type: application/json" \
     -d '{
       "name": "Laravel Backend",
       "description": "API key for Laravel web backend",
       "scopes": ["tasks", "content", "images", "analytics"],
       "allowedIps": ["127.0.0.1", "192.168.1.0/24"]
     }'
```

Response:
```json
{
  "success": true,
  "data": {
    "id": "abc123...",
    "name": "Laravel Backend",
    "keyPrefix": "aim_xxxx",
    "plainKey": "aim_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
    "scopes": ["tasks", "content", "images", "analytics"],
    "isActive": true,
    "createdAt": "2025-12-18T10:00:00Z"
  },
  "message": "API key created successfully. Please save the key as it won't be shown again.",
  "warning": "Store this key securely - it cannot be retrieved later!"
}
```

## Available Scopes

| Scope | Description | Endpoints |
|-------|-------------|-----------|
| `all` | Full access to all endpoints | * |
| `admin` | Administrative operations | `/api-keys/*`, `/admin/*` |
| `tasks` | Task management | `/tasks/*` |
| `workers` | Worker management | `/workers/*` |
| `content` | Content generation | `/content/*`, `/generate/*` |
| `images` | Image generation | `/image/*` |
| `automation` | Web automation | `/automation/*`, `/workflow/*` |
| `analytics` | Analytics access | `/analytics/*`, `/stats/*` |
| `read` | Read-only access | GET requests on analytics |

## Excluded Paths (No Auth Required)

The following paths do not require API key authentication:

- `/swagger` - API documentation
- `/health` - Basic health check
- `/api/status/health` - Detailed health check
- `/hub/aimanager/negotiate` - SignalR negotiation

## IP Restrictions

You can restrict an API key to specific IP addresses:

```json
{
  "name": "Production Server",
  "allowedIps": [
    "192.168.1.100",     // Specific IP
    "10.0.0.0/8",        // CIDR notation
    "*"                   // Allow all (default)
  ]
}
```

## Key Expiration

Set expiration date for API keys:

```bash
curl -X PUT "http://localhost:5000/api/apikeys/{id}" \
     -H "X-API-Key: aim_master_key" \
     -H "Content-Type: application/json" \
     -d '{
       "expiresAt": "2026-01-01T00:00:00Z"
     }'
```

## Integration Examples

### Laravel PHP

```php
// config/aimanager.php
return [
    'auth' => [
        'api_key' => env('AI_MANAGER_API_KEY', ''),
    ],
];

// .env
AI_MANAGER_API_KEY=aim_your_api_key_here
```

```php
// app/Services/AIManagerClient.php
use Illuminate\Support\Facades\Http;

class AIManagerClient
{
    protected function request(string $method, string $endpoint, array $data = []): array
    {
        $response = Http::withHeaders([
                'X-API-Key' => config('aimanager.auth.api_key'),
                'Accept' => 'application/json',
            ])
            ->$method($this->baseUrl . $endpoint, $data);

        return $response->json();
    }
}
```

### Python

```python
import requests

API_KEY = "aim_your_api_key_here"
BASE_URL = "http://localhost:5000/api"

headers = {
    "X-API-Key": API_KEY,
    "Accept": "application/json"
}

# Get health status
response = requests.get(f"{BASE_URL}/status/health", headers=headers)
print(response.json())

# Submit a task
task = {
    "type": "content",
    "platform": "facebook",
    "data": {"message": "Hello World"}
}
response = requests.post(f"{BASE_URL}/tasks", json=task, headers=headers)
print(response.json())
```

### Node.js / TypeScript

```typescript
import axios from 'axios';

const API_KEY = process.env.AI_MANAGER_API_KEY;
const BASE_URL = 'http://localhost:5000/api';

const client = axios.create({
  baseURL: BASE_URL,
  headers: {
    'X-API-Key': API_KEY,
    'Accept': 'application/json'
  }
});

// Get health status
const health = await client.get('/status/health');
console.log(health.data);

// Submit a task
const task = await client.post('/tasks', {
  type: 'content',
  platform: 'facebook',
  data: { message: 'Hello World' }
});
console.log(task.data);
```

### C# / .NET

```csharp
using System.Net.Http;
using System.Net.Http.Json;

var apiKey = Environment.GetEnvironmentVariable("AI_MANAGER_API_KEY");
var client = new HttpClient();

client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
client.DefaultRequestHeaders.Accept.Add(
    new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

// Get health status
var health = await client.GetFromJsonAsync<HealthResponse>(
    "http://localhost:5000/api/status/health");

// Submit a task
var task = new { type = "content", platform = "facebook", data = new { message = "Hello" } };
var response = await client.PostAsJsonAsync("http://localhost:5000/api/tasks", task);
var result = await response.Content.ReadFromJsonAsync<TaskResponse>();
```

### cURL (Bash Script)

```bash
#!/bin/bash

API_KEY="aim_your_api_key_here"
BASE_URL="http://localhost:5000/api"

# Health check
curl -s -H "X-API-Key: $API_KEY" "$BASE_URL/status/health" | jq

# Submit task
curl -s -X POST "$BASE_URL/tasks" \
    -H "X-API-Key: $API_KEY" \
    -H "Content-Type: application/json" \
    -d '{"type":"content","platform":"facebook","data":{"message":"Hello"}}' | jq
```

## Error Responses

### Missing API Key
```json
{
  "success": false,
  "error": "API key is required. Use header 'X-API-Key' or query parameter 'api_key'",
  "code": "UNAUTHORIZED"
}
```

### Invalid API Key
```json
{
  "success": false,
  "error": "Invalid API key",
  "code": "UNAUTHORIZED"
}
```

### Disabled API Key
```json
{
  "success": false,
  "error": "API key is disabled",
  "code": "UNAUTHORIZED"
}
```

### Expired API Key
```json
{
  "success": false,
  "error": "API key has expired",
  "code": "UNAUTHORIZED"
}
```

### IP Not Allowed
```json
{
  "success": false,
  "error": "IP address not allowed",
  "code": "UNAUTHORIZED"
}
```

### Insufficient Scope
```json
{
  "success": false,
  "error": "API key does not have 'admin' scope",
  "code": "UNAUTHORIZED"
}
```

## Security Best Practices

1. **Never commit API keys** - Use environment variables
2. **Use IP restrictions** - Limit keys to known server IPs
3. **Set expiration dates** - Rotate keys periodically
4. **Use minimal scopes** - Only grant necessary permissions
5. **Monitor usage** - Check `usageCount` and `lastUsedAt` regularly
6. **Revoke unused keys** - Delete keys no longer in use
7. **Use HTTPS in production** - Encrypt key transmission

## Admin UI

The WPF desktop application includes an API Keys management page:

1. Navigate to **System > API Keys** in the sidebar
2. Click **Create New Key** to generate a new key
3. Configure name, description, scopes, and IP restrictions
4. Copy the generated key immediately (shown only once)
5. Use the action buttons to edit, regenerate, toggle, or delete keys

## File Storage

API keys are stored in:
- Windows: `%APPDATA%\AIManager\api_keys.json`
- Linux: `~/.config/AIManager/api_keys.json`

Keys are stored with SHA256 hashing - the plain key is never stored after initial generation.
