# UrbanService AI Knowledge Base

Tai lieu nay gom cac noi dung co the seed vao bang `AiKnowledgeSource` de chatbot UrbanService tra loi nguoi dan. Nen tao cac record `SourceType = "Markdown"`, `IsActive = true`; cac muc chung dat `CategoryId = null`, cac muc theo linh vuc gan voi category tuong ung neu database da co.

## Tong quan he thong

UrbanService la he thong tiep nhan, theo doi va ho tro xu ly phan anh do thi cua nguoi dan. Nguoi dan co the gui phan anh kem mo ta, dia diem, muc uu tien va hinh anh. Nhan vien he thong se kiem tra, phan loai, phan cong don vi xu ly, theo doi tien do va cap nhat ket qua cho nguoi dan.

Chatbot UrbanService chi ho tro giai thich quy trinh, trang thai feedback, cach gui phan anh va thong tin chung trong he thong. Chatbot khong duoc hua thoi gian xu ly cu the neu khong co thong tin chinh thuc trong feedback hoac knowledge.

## Quy trinh xu ly feedback

Feedback moi tao se o trang thai `Submitted`. Sau khi nhan vien hoac AI kiem tra noi dung hop le, feedback co the chuyen sang `Verified`. Feedback da verified moi nen duoc phan cong cho don vi hoac operator phu trach.

Sau khi phan cong, feedback chuyen sang `Assigned`. Don vi xu ly dang thuc hien thi feedback co the o trang thai `InProgress`. Khi co ket qua, operator gui ket qua va feedback chuyen sang `SubmittedForApproval` de quan ly duyet.

Neu quan ly chap thuan ket qua, feedback chuyen sang `Approved`. Neu can lam lai, feedback chuyen sang `NeedRework`. Khi nguoi dan xac nhan hoan tat hoac quy trinh ket thuc, feedback chuyen sang `Closed`. Feedback co the bi `Rejected` neu noi dung khong hop le hoac `Cancelled` neu bi huy.

## Huong dan nguoi dan tao feedback

Nguoi dan nen cung cap tieu de ngan gon, mo ta ro van de, dia diem cu the va hinh anh neu co. Hinh anh nen chup ro hien truong, bien bao, ten duong, so nha hoac moc vi tri gan do neu co the.

Khong nen gui thong tin nhay cam nhu mat khau, ma OTP, thong tin tai khoan ngan hang, hinh anh giay to tuy than hoac du lieu ca nhan cua nguoi khac neu khong can thiet cho viec xu ly phan anh.

## Cac loai phan anh moi truong

Phan anh moi truong co the bao gom rac thai do sai noi quy dinh, diem tap ket rac tu phat, mui hoi, nuoc thai, khoi bui, tieng on, cay xanh gay nguy hiem, kenh muong bi o nhiem hoac dong vat chet noi cong cong.

Khi nguoi dan bao cao van de moi truong, chatbot nen khuyen nguoi dan cung cap dia diem, thoi gian phat hien, muc do anh huong, hinh anh hien truong va dau hieu nguy hiem neu co.

Voi cac tinh huong co nguy co khan cap nhu hoa chat tran do, chay no, khoi doc, nuoc thai nguy hai hoac cay do can tro giao thong, chatbot nen khuyen nguoi dan tranh xa khu vuc nguy hiem va lien he kenh khan cap/chinh quyen dia phuong theo quy dinh.

## Cac loai phan anh ha tang do thi

Phan anh ha tang co the gom duong hu hong, o ga mat nap, den duong khong sang, bien bao hong, via he bi lan chiem, ngap nuoc, cap thoat nuoc, cong trinh gay can tro hoac tai san cong bi hu hong.

Nguoi dan nen gui anh chup ro vi tri va mo ta muc do anh huong den di lai, an toan, sinh hoat hoac moi truong xung quanh.

## Nguyen tac tra loi cua chatbot

Chatbot tra loi bang tieng Viet, ngan gon, lich su va de hieu. Chatbot chi dua vao knowledge duoc cung cap va thong tin feedback neu co. Neu khong du thong tin, chatbot phai noi ro chua du thong tin va de xuat nguoi dan theo doi tren he thong hoac lien he nhan vien ho tro.

Chatbot khong tu y ket luan feedback da duoc xu ly neu trang thai feedback khong the hien dieu do. Chatbot khong tu y dua ra cam ket ve thoi gian, boi thuong, xu phat, trach nhiem phap ly hoac quyet dinh hanh chinh.

## Goi y seed SQL

Co the cat tung muc thanh nhieu record ngan de chat search tot hon:

```sql
insert into ai_knowledge_sources (category_id, title, source_type, content, is_active, created_at)
values
(null, 'Tong quan UrbanService', 'Markdown', 'UrbanService la he thong tiep nhan, theo doi va ho tro xu ly phan anh do thi cua nguoi dan...', true, now()),
(null, 'Quy trinh xu ly feedback', 'Markdown', 'Feedback moi tao co trang thai Submitted. Sau khi kiem tra hop le chuyen sang Verified...', true, now()),
(null, 'Nguyen tac tra loi chatbot', 'Markdown', 'Chatbot tra loi bang tieng Viet, ngan gon, lich su, chi dua tren knowledge va thong tin feedback neu co...', true, now());
```
