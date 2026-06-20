# Huong dan ket noi Qwen2.5-VL cho UrbanService BE

Tai lieu nay huong dan ket noi BE `UrbanService` da deploy tren VPS `152.42.177.174` toi AI server chay model `qwen2.5vl:3b` tren VPS `139.59.71.176`.

> Luu y: IP AI ban gui  `139.59.71.176`.

## 0. Tai model qwen2.5vl:3b tren AI VPS

SSH vao AI VPS `139.59.71.176`:

```bash
ssh root@139.59.71.176
```

Cai Ollama neu VPS chua co:

```bash
curl -fsSL https://ollama.com/install.sh | sh
sudo systemctl enable ollama
sudo systemctl start ollama
```

Tai model vision:

```bash
ollama pull qwen2.5vl:3b
ollama list
```

Test model truc tiep tren VPS:

```bash
ollama run qwen2.5vl:3b "Tra loi ngan gon: UrbanService la gi?"
```

Neu server yeu cau Ollama lang nghe qua network de BE goi duoc, tiep tuc lam phan `Mo API tren AI VPS` ben duoi.

## 1. Kien truc de xuat

```text
Citizen/FE
  -> UrbanService API VPS 152.42.177.174
      -> Ollama/Qwen2.5-VL API VPS 139.59.71.176:11434
          -> model qwen2.5vl:3b
```

BE khong nen goi AI tu FE truc tiep. FE chi goi API cua UrbanService. UrbanService chiu trach nhiem:

- Lay feedback, text, attachment image tu database/Cloudinary.
- Goi AI server de phan tich text + anh.
- Luu ket qua vao `AnalysisResult`.
- Doi `Feedback.Status` sang trang thai da review.
- Luu hoi thoai chatbot vao `AiConversation` va `AiMessage`.
- Lay ngu canh tra loi tu `AiKnowledgeSource`.

## 2. Status feedback hop le hien tai

File hien tai: `UrbanService.BLL/Common/Constraint/FeedbackStatus.cs`.

Status hop le:

- `Submitted`
- `Verified`
- `Assigned`
- `InProgress`
- `Resolved`
- `SubmittedForApproval`
- `Approved`
- `Rejected`
- `NeedRework`
- `Closed`
- `Cancelled`

Hien chua co status rieng ten `AIReviewed`. Neu yeu cau la "da duoc AI review" nhung khong muon them migration, nen dung `Verified` va tao lich su status voi note `Reviewed by AI`. Neu muon phan biet ro AI review voi staff verify, hay them status moi:

```csharp
public const string AIReviewed = "AIReviewed";
```

Sau do them vao `Allowed`, tao migration neu database co constraint lien quan status, va cap nhat cac flow dang yeu cau `Verified` neu can.

Khuyen nghi ngan han: dung `Verified` sau khi AI phan tich xong, vi flow assign hien tai dang yeu cau feedback phai o status `Verified`.

## 3. Mo API tren AI VPS

Neu AI server dang dung Ollama, API mac dinh la port `11434`.

Kiem tra model tren AI VPS:

```bash
ollama list
ollama run qwen2.5vl:3b
```

Cho Ollama listen ra network private/public:

```bash
sudo systemctl edit ollama
```

Them noi dung:

```ini
[Service]
Environment="OLLAMA_HOST=0.0.0.0:11434"
```

Restart:

```bash
sudo systemctl daemon-reload
sudo systemctl restart ollama
sudo systemctl status ollama
```

Mo firewall chi cho BE VPS goi vao AI VPS:

```bash
sudo ufw allow from 152.42.177.174 to any port 11434 proto tcp
sudo ufw reload
```

Test tu BE VPS:

```bash
curl http://139.59.71.176:11434/api/tags
```

Neu BE bao loi timeout/khong ket noi toi `139.59.71.176:11434`, kiem tra theo thu tu:

```bash
# Tren AI VPS 139.59.71.176
sudo systemctl status ollama
ss -lntp | grep 11434
curl http://127.0.0.1:11434/api/tags
```

Neu `ss` chi thay Ollama listen `127.0.0.1:11434`, can mo listen ra network:

```bash
sudo systemctl edit ollama
```

Noi dung:

```ini
[Service]
Environment="OLLAMA_HOST=0.0.0.0:11434"
```

Restart va kiem tra lai:

```bash
sudo systemctl daemon-reload
sudo systemctl restart ollama
ss -lntp | grep 11434
```

Mo firewall tren AI VPS chi cho BE VPS goi vao:

```bash
sudo ufw allow from 152.42.177.174 to any port 11434 proto tcp
sudo ufw reload
sudo ufw status
```

Neu VPS dung firewall cua nha cung cap cloud, can mo inbound TCP `11434` cho source IP `152.42.177.174` trong dashboard cloud nua.

Test chat:

```bash
curl http://139.59.71.176:11434/api/chat \
  -H "Content-Type: application/json" \
  -d '{
    "model": "qwen2.5vl:3b",
    "stream": false,
    "messages": [
      {
        "role": "user",
        "content": "Tra loi ngan gon: UrbanService la gi?"
      }
    ]
  }'
```

## 4. Cau hinh BE

Them bien moi vao `.env` hoac environment cua container BE:

```env
AI__BaseUrl=http://139.59.71.176:11434
AI__Model=qwen2.5vl:3b
AI__TimeoutSeconds=120
```

Trong `docker-compose.prod.yml`, them vao `services.backend.environment`:

```yaml
AI__BaseUrl: ${AI_BASE_URL}
AI__Model: ${AI_MODEL:-qwen2.5vl:3b}
AI__TimeoutSeconds: ${AI_TIMEOUT_SECONDS:-120}
```

Trong file `.env` tren VPS BE:

```env
AI_BASE_URL=http://139.59.71.176:11434
AI_MODEL=qwen2.5vl:3b
AI_TIMEOUT_SECONDS=120
```

## 5. API phan tich feedback text + anh

### Endpoint de xuat

Them controller:

```text
POST /api/management/feedbacks/{feedbackId}/ai-analysis
Role: SYSTEMSTAFF, SYSTEMADMIN, INTERACTIONMANAGER
```

Hoac neu muon tu dong phan tich ngay sau khi citizen tao feedback, goi service AI o cuoi `FeedbackService.CreateAsync`.

Khuyen nghi giai doan dau: dung endpoint rieng de test on dinh truoc, sau do moi tu dong hoa.

### Service/file nen them

```text
UrbanService.BLL/DTOs/AI/AiDtos.cs
UrbanService.BLL/Interfaces/IAiClient.cs
UrbanService.BLL/Interfaces/IAiFeedbackAnalysisService.cs
UrbanService.BLL/Interfaces/IAiChatService.cs
UrbanService.BLL/Services/AiClient.cs
UrbanService.BLL/Services/AiFeedbackAnalysisService.cs
UrbanService.BLL/Services/AiChatService.cs
UrbanService/Controllers/AiController.cs
```

Dang ky DI trong `Program.cs`:

```csharp
builder.Services.AddHttpClient<IAiClient, AiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["AI:BaseUrl"]!);
    client.Timeout = TimeSpan.FromSeconds(
        int.Parse(builder.Configuration["AI:TimeoutSeconds"] ?? "120"));
});

builder.Services.AddScoped<IAiFeedbackAnalysisService, AiFeedbackAnalysisService>();
builder.Services.AddScoped<IAiChatService, AiChatService>();
```

### Input cho AI

Lay du lieu tu:

- `Feedback.Title`
- `Feedback.Description`
- `Feedback.LocationText`
- `Feedback.Priority`
- `Feedback.Category.CategoryName`
- `Feedback.FeedbackAttachments` voi `FileType` bat dau bang `image` hoac URL anh.

Voi anh dang la URL Cloudinary, BE nen download image thanh bytes, convert sang base64, roi gui vao Ollama `images`.

### Prompt de xuat cho analysis

AI phai tra ve JSON hop le, khong them markdown:

```text
Ban la he thong phan tich phan anh do thi cho UrbanService.
Hay phan tich feedback cua nguoi dan dua tren text va anh dinh kem.

Tra ve dung JSON:
{
  "detectedCategoryName": string | null,
  "sentiment": "Positive" | "Neutral" | "Negative",
  "urgencyLevel": "Low" | "Medium" | "High" | "Critical",
  "summary": string,
  "keywords": string[],
  "confidenceScore": number,
  "riskNotes": string[]
}

Khong duoc them giai thich ngoai JSON.
Neu anh khong ro hoac khong lien quan, ghi ro trong riskNotes.
```

### Ollama request mau co anh

```json
{
  "model": "qwen2.5vl:3b",
  "stream": false,
  "messages": [
    {
      "role": "user",
      "content": "Phan tich feedback theo schema JSON...",
      "images": ["BASE64_IMAGE_1", "BASE64_IMAGE_2"]
    }
  ],
  "format": "json"
}
```

### Luu vao `AnalysisResult`

Map ket qua AI vao entity hien co:

- `FeedbackId`: feedback dang phan tich.
- `ModelName`: `qwen2.5vl:3b`.
- `DetectedCategoryId`: tim theo `detectedCategoryName` trong `UrbanServiceCategory`; neu khong match thi de `null`.
- `Sentiment`: gia tri AI tra ve.
- `UrgencyLevel`: gia tri AI tra ve.
- `Summary`: tom tat AI tra ve.
- `Keywords`: nen luu JSON string cua mang keywords, vi field hien la `string?`.
- `ConfidenceScore`: decimal tu 0 den 1.
- `RawResponse`: raw JSON AI tra ve.
- `CreatedAt`: `DateTime.UtcNow`.

Sau khi luu `AnalysisResult`, cap nhat `Feedback.Status`:

- Khuyen nghi: `Verified`
- Tao `FeedbackStatusHistory`:
  - `OldStatus`: status cu
  - `NewStatus`: `Verified`
  - `ChangedByUserId`: user dang goi API, hoac system AI user neu co
  - `Note`: `Reviewed by AI using qwen2.5vl:3b`
  - `ChangedAt`: `DateTime.UtcNow`

Nen boc trong transaction: luu `AnalysisResult`, cap nhat `Feedback`, them `FeedbackStatusHistory`, roi `SaveAsync`.

## 6. Chatbot voi nguoi dan

### Entity hien co

Hien repo co:

- `AiConversation`
- `AiMessage`
- `AiKnowledgeSource`

Khong thay entity ten `AIKnowledgeResource`. Neu frontend/spec dang goi la `AIKnowledgeResource`, co 2 cach:

- Dung lai `AiKnowledgeSource` va doi ten trong tai lieu/API thanh knowledge source.
- Hoac them entity/table moi `AiKnowledgeResource` neu bat buoc can dung dung ten. Hien tai chua can them neu muc tieu chi la chatbot co knowledge base.

### Endpoint de xuat

```text
POST /api/ai/chat
Role: SERVICEUSER
Body:
{
  "conversationId": 123,
  "feedbackId": "optional-guid",
  "message": "Toi muon hoi ve tien do phan anh"
}
```

Neu `conversationId` null, tao `AiConversation` moi:

- `UserId`: current user id
- `FeedbackId`: optional
- `Title`: lay 80 ky tu dau tu message
- `StartedAt`: `DateTime.UtcNow`
- `Status`: `Active`

Luu message user vao `AiMessage`:

- `SenderType`: `User`
- `MessageText`: message
- `CreatedAt`: `DateTime.UtcNow`

Lay knowledge:

- `AiKnowledgeSource.IsActive == true`
- Neu co `feedbackId`, uu tien knowledge co `CategoryId == feedback.CategoryId`
- Lay them knowledge `CategoryId == null` lam rule chung
- Neu content dai, chi lay top N doan lien quan bang search text don gian truoc; sau co the nang cap vector search.

Prompt chatbot:

```text
Ban la tro ly UrbanService cho nguoi dan.
Chi tra loi dua tren knowledge duoc cung cap va thong tin feedback neu co.
Neu khong du thong tin, hay noi ro la chua du thong tin va de xuat nguoi dan lien he nhan vien ho tro.
Khong tu y hua thoi gian xu ly neu knowledge khong co.
Tra loi bang tieng Viet, ngan gon, lich su.
```

Sau khi AI tra loi, luu message AI vao `AiMessage`:

- `SenderType`: `AI`
- `MessageText`: response text
- `CreatedAt`: `DateTime.UtcNow`

Tra ve FE:

```json
{
  "conversationId": 123,
  "message": "Cau tra loi cua AI",
  "createdAt": "2026-06-20T00:00:00Z"
}
```

## 7. Cac diem can bo sung neu thieu

Nen them:

- DTO rieng cho AI analysis request/response va chat request/response.
- `IAiClient` de gom logic goi Ollama `/api/chat`.
- Timeout dai hon request HTTP binh thuong, toi thieu 120 giay cho model vision 3B.
- Retry nhe cho loi network tam thoi, nhung khong retry qua nhieu vi analysis co the ton GPU/CPU.
- Logging raw error tu AI server, khong tra raw exception ve FE.
- Health endpoint noi bo: `GET /api/ai/health` de test BE co ket noi duoc AI VPS.

Co the chua can them:

- `AIKnowledgeResource`, vi DB/code hien co da co `AiKnowledgeSource` phu hop knowledge base.
- Vector database. Giai doan dau co the search theo category + keyword tren `Content`.

## 8. Bao mat va deploy

- Khong public port `11434` cho tat ca internet. Chi allow tu IP BE `152.42.177.174`.
- Neu co Nginx truoc Ollama, dat basic auth hoac internal network rule.
- Khong de FE goi thang `139.59.71.176:11434`.
- Log request AI nen an bot URL anh private/token neu co.
- Gioi han kich thuoc anh download tu Cloudinary truoc khi convert base64.
- Nen resize/compress anh truoc khi gui AI de tranh request qua lon.

## 9. Thu tu implement khuyen nghi

1. Kiem tra Ollama tren AI VPS bang `/api/tags`.
2. Them config `AI__BaseUrl`, `AI__Model`, `AI__TimeoutSeconds` vao BE deploy.
3. Tao `IAiClient` va test endpoint health.
4. Tao endpoint phan tich feedback, chi luu `AnalysisResult` truoc.
5. Sau khi parse JSON on dinh, cap nhat status sang `Verified` va them `FeedbackStatusHistory`.
6. Tao endpoint chatbot, luu `AiConversation` va `AiMessage`.
7. Seed du lieu `AiKnowledgeSource` de chatbot co ngu canh.
8. Sau khi on dinh, can nhac auto-run AI analysis ngay sau `CreateFeedback`.
