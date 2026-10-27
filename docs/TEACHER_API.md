# API chức năng Teacher

Tất cả API dưới đây cần header `Authorization: Bearer <token>` của tài khoản **teacher**
(đăng nhập bằng `POST /api/auth/login`, tài khoản demo `teacher@demo.com` / `123456`).
Giáo viên chỉ thấy và thao tác được trên **lớp mình phụ trách**.

Lỗi trả về dạng `{ "message": "..." }` (400 sai dữ liệu, 403 không có quyền, 404 không tìm thấy,
409 xung đột nghiệp vụ, 423 điểm danh đã khóa).

Ngày giờ: dùng **giờ địa phương**, không có `Z` ở cuối. Ví dụ `"2026-11-05T23:59:00"`, ngày `"2026-11-05"`, giờ `"18:00:00"`.

## Cài đặt lần đầu (bắt buộc)

Code có thêm nhiều bảng mới nên phải tạo migration:

```powershell
cd EnglishCenter.Api\EnglishCenter.Api
dotnet ef migrations add TeacherFeatures
dotnet ef database update
dotnet run
```

Khi chạy ở Development, hệ thống tự tạo dữ liệu demo cho `teacher@demo.com`: syllabus 12 bài, 1 bài Writing
học viên đã nộp (chờ chấm), 1 bài Quiz, 1 yêu cầu đặt lịch 1 kèm 1 và ngân hàng nhận xét mẫu.

## Khái niệm chính

| Khái niệm | Giải thích |
|---|---|
| **Buổi học (ClassSession)** | Buổi cụ thể có ngày, sinh tự động từ lịch tuần `ClassSchedules` do Admin tạo (đủ `TotalSessions` của khóa). Tự đồng bộ mỗi khi giáo viên mở lịch / lớp. |
| **Buổi bù (Makeup)** | Tạo ra khi Admin duyệt đơn xin nghỉ / đổi buổi có đề xuất buổi bù. |
| **Buổi 1 kèm 1** | Tạo ra khi giáo viên duyệt yêu cầu đặt lịch của học viên. |
| **Trạng thái buổi** | `Upcoming` sắp tới · `Ongoing` đang dạy · `Done` đã xong · `Cancelled` đã hủy |
| **Điểm danh** | `Present` có mặt · `Late` muộn · `Absent` vắng · `Excused` vắng có phép. Khóa sau 48 giờ kể từ khi buổi kết thúc. |
| **Bài nộp** | `Submitted` chờ chấm · `Draft` đã chấm nháp · `Published` đã công bố · `Returned` trả bài cho sửa. Chưa chấm sau 24 giờ = trễ hạn chấm. |

---

## 1. Dashboard

| Method | URL | Mô tả |
|---|---|---|
| GET | `/api/teacher/dashboard` | 4 thẻ số liệu (`stats`), lịch dạy hôm nay (`todaySchedule`), bài chờ chấm + trễ 24h (`grading`), học viên cần chú ý (`attentionStudents`: vắng ≥ 2 buổi liên tiếp, chưa nộp ≥ 2 bài, band gần nhất giảm), yêu cầu chờ xử lý, chuông thông báo, lối tắt (`shortcuts.attendanceSessionId`, `shortcuts.nextSubmissionId`) |

## 2. Lịch dạy

| Method | URL | Mô tả |
|---|---|---|
| GET | `/api/teacher/schedule?view=week&date=2026-11-04` | Lịch tuần (Thứ 2 → CN). `view=month` = lịch tháng. Hoặc `from=&to=`. Thêm `includeAvailability=true` để lấy cả lịch rảnh. Trả `events` (mỗi buổi: `kind` = Class / Makeup / OneOnOne, giờ, lớp, phòng, sĩ số, `status`), `days` (nhóm theo ngày, dùng cho giao diện điện thoại), `previous` / `next` để chuyển tuần / tháng |
| GET | `/api/teacher/schedule/leave-requests?status=` | Đơn xin nghỉ / đổi buổi đã gửi + trạng thái |
| POST | `/api/teacher/schedule/leave-requests` | Gửi đơn: `{ "type": "Leave" \| "Reschedule", "sessionId", "reason", "proposedDate", "proposedStartTime", "proposedEndTime" }` |
| DELETE | `/api/teacher/schedule/leave-requests/{id}` | Hủy đơn đang chờ |
| GET | `/api/teacher/schedule/availability?from=&to=` | Lịch rảnh đã khai báo |
| POST | `/api/teacher/schedule/availability` | Khai báo khung giờ rảnh: `{ "date", "startTime", "endTime", "note", "repeatWeeks": 1-12 }` |
| DELETE | `/api/teacher/schedule/availability/{id}` | Xóa khung giờ (chưa có ai đặt) |

## 3. Lớp của tôi

| Method | URL | Mô tả |
|---|---|---|
| GET | `/api/teacher/classes?status=Upcoming\|Ongoing\|Finished` | Danh sách lớp: khóa, lịch học, thời gian, sĩ số `enrolledCount/maxStudents`, `progressPercent`, trạng thái |
| GET | `/api/teacher/classes/{id}` | Chi tiết lớp + danh sách học viên (mã, tên, SĐT, email, trạng thái, tỉ lệ chuyên cần) + `defaultSessionId` |
| GET | `/api/teacher/classes/{id}/sessions` | Danh sách buổi học của lớp |
| PUT | `/api/teacher/classes/{id}/meeting-url` | Link Zoom / Meet của cả lớp: `{ "meetingUrl" }` |
| GET | `/api/teacher/classes/{id}/syllabus` | Giáo án: các bài của khóa, buổi nào đã gắn, đã dạy, `completionPercent` |
| POST / PUT / DELETE | `/api/teacher/classes/{id}/syllabus[/{lessonId}]` | Thêm / sửa / xóa bài: `{ "orderNo", "title", "description" }` |
| GET | `/api/teacher/sessions/{id}` | Chi tiết buổi: bài giáo án, tài liệu, link online, nhật ký, tóm tắt điểm danh |
| PUT | `/api/teacher/sessions/{id}/lesson` | Gắn bài và đánh dấu đã dạy: `{ "syllabusLessonId", "isTaught" }` |
| PUT | `/api/teacher/sessions/{id}/log` | Nhật ký buổi: `{ "contentTaught", "homework" }` |
| PUT | `/api/teacher/sessions/{id}/meeting-url` | Link online riêng của buổi |
| POST | `/api/teacher/sessions/{id}/materials` | Thêm tài liệu: `{ "title", "kind": "Pdf\|Audio\|Video\|Link\|Other", "url" }` |
| DELETE | `/api/teacher/sessions/{id}/materials/{materialId}` | Xóa tài liệu |
| POST | `/api/teacher/files?folder=materials` | Upload file (multipart, field `file`, tối đa 30 MB) → trả `url` dạng `/uploads/...` |

Gửi thông báo cho cả lớp: xem mục 9 (`POST /api/teacher/announcements`).

## 4. Điểm danh

| Method | URL | Mô tả |
|---|---|---|
| GET | `/api/teacher/classes/{classId}/attendance/default` | Buổi mặc định cần điểm danh (buổi gần nhất) |
| GET | `/api/teacher/sessions/{id}/attendance` | Bảng điểm danh: học viên, trạng thái, gợi ý `Excused` nếu đơn xin nghỉ đã duyệt, `locked`, `lockAt`, `summary` |
| PUT | `/api/teacher/sessions/{id}/attendance` | Lưu: `{ "items": [ { "studentId", "status", "note" } ] }`. Phải đủ mọi học viên đang học. Nút "Tất cả có mặt" = gửi tất cả `Present`. Trả về `warnings` (học viên vừa vượt 20% vắng) |
| POST | `/api/teacher/sessions/{id}/attendance/unlock-request` | Xin Admin mở lại khi đã khóa: `{ "reason" }` |
| GET | `/api/teacher/classes/{classId}/attendance` | Bảng tổng hợp chuyên cần cả lớp (cột tỉ lệ %, cảnh báo > 20%) |

## 5. Bài tập

| Method | URL | Mô tả |
|---|---|---|
| GET | `/api/teacher/assignments?classId=&skill=&state=open\|closed` | Danh sách: tên, lớp, kỹ năng, hạn nộp, `submittedCount/assignedCount` |
| GET | `/api/teacher/assignments/{id}` | Chi tiết (kèm câu hỏi + đáp án nếu là Quiz) |
| POST | `/api/teacher/assignments` | Giao bài (body bên dưới) |
| PUT | `/api/teacher/assignments/{id}` | Sửa (đã có bài nộp thì không đổi lớp, kỹ năng, loại bài, câu hỏi) |
| DELETE | `/api/teacher/assignments/{id}` | Xóa (chặn nếu đã có học viên nộp) |
| GET | `/api/teacher/assignments/{id}/missing` | Học viên chưa nộp |
| POST | `/api/teacher/assignments/{id}/remind` | Nhắc học viên chưa nộp |

```json
{
  "classId": 1, "skill": "Writing", "type": "Writing", "title": "Task 2 - Education",
  "prompt": "Some people think...", "dueAt": "2026-11-10T23:59:00", "minWords": 250,
  "attachmentUrl": null, "note": null, "allowLate": true,
  "assignToAll": false, "studentIds": [1, 2],
  "questions": [],
  "examId": null
}
```

`type`: `Writing` (kỹ năng Writing) · `Speaking` (thu âm) · `Quiz` (Reading / Listening, tự chấm). Câu hỏi Quiz:
`{ "questionType": "MultipleChoice", "content": "...", "options": ["A. ...", "B. ..."], "correctAnswer": "B", "points": 1 }`
hoặc `{ "questionType": "FillBlank", "content": "...", "correctAnswer": "colour|color" }` (nhiều đáp án đúng ngăn bằng `|`).

## 6. Chấm bài

| Method | URL | Mô tả |
|---|---|---|
| GET | `/api/teacher/grading/submissions?status=pending\|overdue\|graded\|returned&classId=&skill=` | Danh sách bài nộp (lần nộp mới nhất), nộp sớm nhất lên đầu, có `gradingOverdue` |
| GET | `/api/teacher/grading/summary` | Số bài chờ chấm, trễ 24h |
| GET | `/api/teacher/grading/submissions/{id}` | Trang chấm: đề, bài viết, `wordCount`, `minWordsWarning`, audio, tiêu chí, highlight, lịch sử các lần nộp, `nextSubmissionId` |
| PUT | `/api/teacher/grading/submissions/{id}` | Lưu nháp: `{ "tr", "cc", "lr", "gra" }` (Writing) hoặc `{ "fc", "lr", "gra", "p" }` (Speaking), thang 0-9 bước 0.5, `feedback`, `feedbackAudioUrl`, `annotations: [{ "startOffset", "endOffset", "selectedText", "errorType": "Grammar\|Vocabulary\|Coherence", "suggestion", "comment" }]`. Band tổng tự tính |
| POST | `/api/teacher/grading/submissions/{id}/publish` | Công bố (học viên mới thấy điểm) |
| POST | `/api/teacher/grading/submissions/{id}/return` | Trả bài cho sửa: `{ "message" }` → học viên nộp lần 2, 3... |
| GET | `/api/teacher/grading/next?currentId=` | Bài tiếp theo cần chấm |
| GET | `/api/teacher/grading/band-descriptors?skill=Writing` | Mô tả band từng tiêu chí |
| GET / POST / DELETE | `/api/teacher/grading/comment-templates` | Ngân hàng nhận xét mẫu (mẫu chung + mẫu của mình) |

Ghi âm nhận xét Speaking: frontend thu âm (MediaRecorder), upload bằng `POST /api/teacher/files?folder=feedback`, rồi gửi `url` vào `feedbackAudioUrl`.

## 7. Kiểm tra & Sổ điểm

| Method | URL | Mô tả |
|---|---|---|
| GET | `/api/teacher/exams?classId=&type=` | Danh sách bài kiểm tra |
| GET | `/api/teacher/exams/{id}` | Chi tiết + điểm từng học viên + phần thi online |
| POST / PUT / DELETE | `/api/teacher/exams[/{id}]` | `{ "classId", "title", "type": "Periodic\|Midterm\|Final", "skill", "examDate", "durationMinutes", "maxScore" }` |
| PUT | `/api/teacher/exams/{id}/results` | Nhập điểm tay (thi giấy): `{ "items": [ { "studentId", "score", "note" } ] }` |
| POST | `/api/teacher/exams/{id}/sync` | Tính lại điểm từ phần thi online |
| GET | `/api/teacher/classes/{classId}/gradebook` | Sổ điểm: cột bài tập + bài kiểm tra, điểm tổng kết, Đạt / Không đạt |
| GET | `/api/teacher/classes/{classId}/gradebook/export` | Tải file Excel |
| GET / PUT | `/api/teacher/classes/{classId}/grade-config` | Trọng số: `{ "assignmentWeight": 20, "periodicWeight": 0, "midtermWeight": 30, "finalWeight": 50, "passScore": 5 }` (tổng = 100) |

Phần thi online: tạo bài tập ở mục 5 với `examId` = id bài kiểm tra. Quiz tự chấm, Writing/Speaking chấm ở page Chấm bài, điểm tự đổ về bài kiểm tra (quy theo `maxScore`). Điểm nhập tay không bị ghi đè.

## 8. Học viên

| Method | URL | Mô tả |
|---|---|---|
| GET | `/api/teacher/students?search=&classId=` | Học viên của các lớp mình dạy |
| GET | `/api/teacher/students/{id}` | Hồ sơ tiến bộ: `bandHistory` (biểu đồ band), `targetBand`, chuyên cần từng lớp, `examHistory`, các bài đã nộp |
| PUT | `/api/teacher/students/{id}/target-band` | `{ "targetBand": 6.5 }` |
| GET / POST / PUT / DELETE | `/api/teacher/students/{id}/reviews[/{reviewId}]` | Nhận xét định kỳ: `{ "classId", "period": "Monthly\|MidCourse\|EndCourse", "periodLabel", "attitude", "attendanceComment", "strengths", "improvements", "listeningComment", "readingComment", "writingComment", "speakingComment", "overallComment" }` |
| GET | `/api/teacher/students/{id}/final-report?classId=` | Phiếu cuối khóa (JSON tổng hợp) |
| GET | `/api/teacher/students/{id}/final-report/print?classId=` | Phiếu cuối khóa dạng HTML để in / lưu PDF (frontend fetch kèm token rồi mở cửa sổ mới, gọi `window.print()`) |
| POST | `/api/teacher/students/{id}/promotions` | Đề xuất lên lớp: `{ "classId", "recommendedCourseId", "reason" }` |
| GET | `/api/teacher/students/promotions` | Các đề xuất đã gửi |

## 9. Yêu cầu & Thông báo

| Method | URL | Mô tả |
|---|---|---|
| GET | `/api/teacher/requests?status=Pending&type=` | Hộp yêu cầu từ học viên |
| POST | `/api/teacher/requests/{id}/approve` | Duyệt `{ "note" }`. Xin nghỉ → điểm danh tự đánh "Vắng có phép". Đặt 1 kèm 1 → tạo buổi trên lịch |
| POST | `/api/teacher/requests/{id}/reject` | Từ chối `{ "reason" }` (bắt buộc) |
| GET | `/api/teacher/requests/one-on-one` | Các buổi 1 kèm 1 |
| PUT | `/api/teacher/requests/one-on-one/{id}/meeting-url` | Link online cho buổi 1 kèm 1 |
| POST | `/api/teacher/announcements` | Gửi thông báo: `{ "classId", "studentIds": [], "title", "content" }` (chỉ `classId` = cả lớp) |
| GET | `/api/teacher/announcements?classId=` | Lịch sử thông báo đã gửi (kèm số người đã đọc) |
| GET | `/api/teacher/notifications?unreadOnly=` | Thông báo gửi đến giáo viên |
| GET | `/api/teacher/notifications/unread-count` | Số trên chuông |
| POST | `/api/teacher/notifications/{id}/read`, `/api/teacher/notifications/read-all` | Đánh dấu đã đọc |

## 10. Hồ sơ

| Method | URL | Mô tả |
|---|---|---|
| GET | `/api/teacher/profile` | Họ tên, email, SĐT, chuyên môn, bằng cấp, ngày vào làm, ảnh |
| POST | `/api/teacher/profile/avatar` | Đổi ảnh (multipart, field `file`, jpg/png/webp ≤ 5 MB) |
| GET | `/api/teacher/profile/classes` | Lịch sử lớp đã / đang dạy |
| GET | `/api/teacher/profile/ratings` | Đánh giá từ học viên: điểm TB, phân bố sao, nhận xét |
| GET | `/api/teacher/profile/timesheet?month=2026-11` | Bảng công: số buổi, số giờ (tính từ buổi đã điểm danh + 1 kèm 1) |
| POST | `/api/teacher/profile/change-password` | `{ "currentPassword", "newPassword", "confirmPassword" }` (≥ 8 ký tự). Trả `token` mới |
| POST | `/api/teacher/profile/logout` | Đăng xuất (token hiện tại hết hiệu lực) |

## Admin duyệt đơn của giáo viên

| Method | URL | Mô tả |
|---|---|---|
| GET | `/api/admin/teacher-requests?status=Pending` | Danh sách đơn |
| POST | `/api/admin/teacher-requests/{id}/approve` | Duyệt. Xin nghỉ / đổi buổi → buổi cũ bị hủy, tạo buổi bù (nếu có đề xuất), báo học viên. Mở lại điểm danh → cho sửa trong 24 giờ |
| POST | `/api/admin/teacher-requests/{id}/reject` | Từ chối `{ "reason" }` |

## Phần dành cho nhóm Student (dữ liệu dùng chung)

Các bảng sau do bên Student ghi dữ liệu, Teacher đọc / xử lý:

- **Nộp bài**: gọi `SubmissionService.SubmitAsync(studentId, assignmentId, content, audioUrl, answers)`. Hàm đã xử lý hạn nộp / nộp trễ, lần nộp, đếm từ, tự chấm Quiz và báo giáo viên.
- **Yêu cầu** (`StudentRequests`): `Type` = `Leave` / `Reschedule` (kèm `SessionId`), `OneOnOneBooking` (kèm `AvailabilityId`, hoặc `Date` + `StartTime` + `EndTime`), `OneOnOneCancel` (kèm `OneOnOneSessionId`). `TeacherId` = giáo viên của lớp.
- **Lịch rảnh giáo viên** để đặt 1 kèm 1: `TeacherAvailabilities` với `IsBooked = false`.
- **Đánh giá giáo viên** (`TeacherRatings`): `Score` 1-5, `Comment`.
- **% hoàn thành khóa**: số buổi `IsTaught = true` / số bài syllabus.
- **Link "Tham gia ngay"**: `ClassSession.MeetingUrl`, để trống thì dùng `Classes.MeetingUrl`.
- **Thông báo**: `Notifications` với `RecipientUserId` = UserId của học viên.
