# AI Content Generation Integration

## Overview

MyPostXAgent ตอนนี้มีระบบ AI Content Generation ที่สมบูรณ์แล้ว รองรับ AI Providers หลายตัว พร้อม fallback อัตโนมัติ

## AI Providers รองรับ

### 1. Ollama (Free, Local)
- **ข้อดี**: ฟรี, ทำงาน offline, ไม่มี rate limit
- **ข้อเสีย**: ต้องติดตั้ง Ollama และ download model
- **Model แนะนำ**: `llama3.2` (default)
- **การติดตั้ง**:
  ```bash
  # ดาวน์โหลด Ollama จาก https://ollama.ai
  ollama pull llama3.2
  ollama serve
  ```
- **URL**: `http://localhost:11434` (default)

### 2. Google Gemini (Free tier + Paid)
- **ข้อดี**: Free tier ใจกว้าง, รองรับภาษาไทยดี
- **ข้อเสีย**: ต้องมี API key
- **Model**: `gemini-2.0-flash-exp` (latest)
- **API Key**: รับได้ฟรีที่ [Google AI Studio](https://makersuite.google.com/app/apikey)
- **Free tier**: 60 requests/minute

### 3. OpenAI GPT (Paid)
- **ข้อดี**: คุณภาพดีมาก, รวดเร็ว
- **ข้อเสีย**: เสียเงิน ($)
- **Model**: `gpt-4o-mini` (ถูกที่สุด)
- **API Key**: รับที่ [OpenAI Platform](https://platform.openai.com/api-keys)
- **ราคา**: ~$0.15/1M input tokens, $0.60/1M output tokens

### 4. Anthropic Claude (Paid)
- **ข้อดี**: คุณภาพดี, เข้าใจ context ยาว
- **ข้อเสีย**: เสียเงิน ($)
- **Model**: `claude-3-5-haiku-20241022` (ถูกที่สุด)
- **API Key**: รับที่ [Anthropic Console](https://console.anthropic.com/)
- **ราคา**: ~$0.80/1M input tokens, $4.00/1M output tokens

## การตั้งค่า

### 1. เปิดหน้า Settings (⚙️)

### 2. กรอก API Keys ตามที่มี:

**AI Content Generation:**
- OpenAI API Key: `sk-...`
- Claude API Key: `sk-ant-...`
- Google API Key: `AIza...`
- Ollama Base URL: `http://localhost:11434`

### 3. กดปุ่ม "💾 บันทึก"
- ระบบจะ reinitialize AI providers อัตโนมัติ

## การใช้งาน Content Generator

### 1. เปิดหน้า "🤖 สร้างเนื้อหา AI"

### 2. เลือก AI Provider:
- ☑️ Ollama (แนะนำ: ฟรี, ไม่มี limit)
- ☑️ OpenAI GPT
- ☑️ Claude
- ☑️ Gemini

### 3. ตั้งค่าเนื้อหา:
- **หัวข้อ**: เช่น "โปรโมทร้านกาแฟ"
- **ประเภท**: โพสต์โปรโมท / Storytelling / รีวิว / etc.
- **โทนเสียง**: Friendly / Professional / Humorous / etc.
- **ความยาว**: สั้น / ปานกลาง / ยาว / ยาวมาก
- **ภาษา**: ไทย / English / ผสม
- **Keywords**: เช่น "กาแฟ, คาเฟ่, ของว่าง"
- **แพลตฟอร์ม**: Facebook / Instagram / TikTok / etc.

### 4. ตัวเลือกเพิ่มเติม:
- ☑️ ใส่ Emojis
- ☑️ ใส่ Call-to-Action

### 5. กดปุ่ม "✨ สร้างเนื้อหา"

### 6. ผลลัพธ์:
- เนื้อหาที่ AI สร้าง (พร้อม emoji, CTA)
- Hashtags ที่เกี่ยวข้อง
- สถิติ: จำนวนตัวอักษร, คำ

### 7. ตัวเลือกหลังสร้าง:
- **🔄 สร้างใหม่**: Generate ใหม่อีกรอบ
- **📋 คัดลอก**: Copy เนื้อหา + hashtags
- **💾 บันทึก Draft**: บันทึกเป็น draft
- **✅ สร้างโพสต์**: สร้างโพสต์และตั้งเวลา

## Fallback System (อัตโนมัติ)

ถ้า AI Provider ที่เลือกไม่พร้อม ระบบจะลอง providers อื่นตามลำดับ:

1. **Ollama** (ถ้าติดตั้งแล้ว)
2. **Gemini** (ถ้ามี API key)
3. **OpenAI** (ถ้ามี API key)
4. **Claude** (ถ้ามี API key)

## Architecture

### Files สร้างใหม่:

```
MyPostXAgent.Core/
├── Models/
│   └── AIModels.cs                          # ContentGenerationRequest, Result, ProviderStatus
├── Services/AI/
│   ├── IAIContentGenerator.cs               # Interface สำหรับ AI providers
│   ├── OllamaContentGenerator.cs            # Ollama implementation
│   ├── OpenAIContentGenerator.cs            # OpenAI GPT implementation
│   ├── ClaudeContentGenerator.cs            # Claude implementation
│   ├── GeminiContentGenerator.cs            # Gemini implementation
│   └── AIContentService.cs                  # Main service (รวม fallback)

MyPostXAgent.UI/
├── ViewModels/
│   ├── ContentGeneratorViewModel.cs         # Updated: ใช้ AI จริง
│   └── SettingsViewModel.cs                 # Updated: Reinitialize AI on save
└── App.xaml.cs                               # Updated: Register AI services
```

### Flow:

```
User Input → ContentGeneratorViewModel
    ↓
    → AIContentService.GenerateContentAsync()
        ↓
        → Try Preferred Provider (Ollama/OpenAI/Claude/Gemini)
        ↓ (if fails)
        → Try Fallback Providers
        ↓
        → Return ContentGenerationResult
    ↓
Display Content + Hashtags
```

## Testing

### ทดสอบ Ollama (Local):

1. ติดตั้ง Ollama:
   ```bash
   # Windows: ดาวน์โหลดจาก https://ollama.ai
   ollama pull llama3.2
   ollama serve
   ```

2. เปิด MyPostXAgent
3. เลือก ☑️ Ollama
4. กรอกหัวข้อ: "รีวิวร้านอาหารอร่อย"
5. กด "✨ สร้างเนื้อหา"
6. ควรได้เนื้อหาภาษาไทยที่สมบูรณ์

### ทดสอบ Gemini (Free API):

1. รับ API key ฟรีจาก [Google AI Studio](https://makersuite.google.com/app/apikey)
2. ไปหน้า Settings (⚙️)
3. กรอก "Google API Key"
4. กด "💾 บันทึก"
5. ไปหน้า "🤖 สร้างเนื้อหา AI"
6. เลือก ☑️ Gemini
7. กรอกหัวข้อและกด "✨ สร้างเนื้อหา"

### ทดสอบ Fallback:

1. เลือก AI provider ที่ไม่มี API key
2. กด "✨ สร้างเนื้อหา"
3. ระบบควรลอง fallback ไป Ollama หรือ provider ที่พร้อมใช้งาน

## Error Handling

### กรณีที่ AI ไม่สำเร็จ:

- **Ollama not running**: แสดงข้อความ "Cannot connect to Ollama"
- **Invalid API Key**: แสดงข้อความ "Gemini/OpenAI/Claude API error"
- **All providers failed**: แสดงข้อความ "All AI providers failed. Please check your settings."

### Debug Mode:

เปิด Output window ใน Visual Studio เพื่อดู logs:
```
Content generated successfully using Gemini
Failed with OpenAI: Invalid API key
Trying fallback: Ollama
```

## Performance

| Provider | Speed | Quality | Cost | Offline |
|----------|-------|---------|------|---------|
| Ollama   | ⭐⭐⭐ | ⭐⭐⭐ | FREE | ✅ |
| Gemini   | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | FREE (tier) | ❌ |
| OpenAI   | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | $$$ | ❌ |
| Claude   | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | $$$$ | ❌ |

## Troubleshooting

### Ollama ไม่ทำงาน:
```bash
# Check if running
curl http://localhost:11434/api/tags

# Restart
ollama serve
```

### API Key ไม่ถูกต้อง:
- ตรวจสอบว่า key ไม่มีช่องว่างหน้า/หลัง
- ตรวจสอบ key ไม่ expired
- ตรวจสอบมี billing account (สำหรับ OpenAI/Claude)

### เนื้อหาไม่เป็นภาษาไทย:
- เลือก "ภาษา: ไทย" ในหน้า Content Generator
- ลองใช้ Gemini (รองรับภาษาไทยดีที่สุด)

## Next Steps

- [ ] เพิ่ม AI Image Generation
- [ ] เพิ่ม AI Video Generation
- [ ] เพิ่ม prompt templates
- [ ] เพิ่ม content history/favorites
- [ ] เพิ่ม batch generation (สร้างหลายโพสต์พร้อมกัน)

---

**Version**: 1.0.0
**Updated**: 26 December 2025
