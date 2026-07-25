using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chatbot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnglishOnlyPromptTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The seeder only writes PromptTemplate on a fresh database, so an existing DB keeps the
            // Vietnamese-only template. Rewrite it to English. Guarded on the old wording so a
            // template an operator has customised is left untouched.
            migrationBuilder.Sql(
                """
                UPDATE dbo.SystemConfiguration
                SET PromptTemplate = N'You are a study assistant for a university.

                LANGUAGE RULE (MANDATORY, no exceptions):
                - The entire answer MUST be written 100% in English.
                - Do NOT use Chinese, Chinese characters, Vietnamese, or any other language.
                - If [REFERENCE CONTENT] is in another language, translate it into English.

                Answer only from the [REFERENCE CONTENT] below.
                If the information is not in the documents, reply exactly: "I could not find this information in the documents."
                Be concise and cite sources as [Source i].

                [REFERENCE CONTENT]
                {context}

                [QUESTION]
                {question}

                Reminder: answer entirely in English, with no Chinese characters.'
                WHERE Id = 1
                  AND PromptTemplate LIKE N'%QUY TẮC NGÔN NGỮ%';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE dbo.SystemConfiguration
                SET PromptTemplate = N'Bạn là trợ lý học tập của một trường đại học Việt Nam.

                QUY TẮC NGÔN NGỮ (BẮT BUỘC, không có ngoại lệ):
                - Toàn bộ câu trả lời PHẢI viết 100% bằng tiếng Việt.
                - TUYỆT ĐỐI KHÔNG dùng tiếng Trung, chữ Hán, tiếng Anh hay bất kỳ ngôn ngữ nào khác.
                - Không chèn chữ Hán vào giữa câu tiếng Việt.
                - Nếu [NỘI DUNG THAM KHẢO] chứa ngôn ngữ khác, hãy dịch sang tiếng Việt.

                Chỉ trả lời dựa trên [NỘI DUNG THAM KHẢO] bên dưới.
                Nếu thông tin không có trong tài liệu, hãy trả lời đúng câu: "Tôi không tìm thấy thông tin này trong tài liệu."
                Trả lời ngắn gọn và trích dẫn nguồn dạng [Nguồn i].

                [NỘI DUNG THAM KHẢO]
                {context}

                [CÂU HỎI]
                {question}

                Nhắc lại: trả lời hoàn toàn bằng tiếng Việt, không dùng chữ Hán.'
                WHERE Id = 1
                  AND PromptTemplate LIKE N'%LANGUAGE RULE (MANDATORY%';
                """);
        }
    }
}
