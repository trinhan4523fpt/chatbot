using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chatbot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VietnameseOnlyPromptTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The seeder only writes PromptTemplate when the singleton row is first created, so
            // existing databases keep the old template, whose language rule was too weak to stop
            // qwen2.5 from drifting into Chinese. Rewrite it in place.
            // Guarded on the old wording so an operator-customised template is left untouched.
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
                  AND PromptTemplate LIKE N'%Trả lời bằng tiếng Việt, ngắn gọn, và trích dẫn nguồn dạng [[]Nguồn i].%';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE dbo.SystemConfiguration
                SET PromptTemplate = N'Bạn là trợ lý học tập. Chỉ trả lời dựa trên [NỘI DUNG THAM KHẢO] bên dưới.
                Nếu thông tin không có trong tài liệu, hãy trả lời đúng câu: "Tôi không tìm thấy thông tin này trong tài liệu."
                Trả lời bằng tiếng Việt, ngắn gọn, và trích dẫn nguồn dạng [Nguồn i].

                [NỘI DUNG THAM KHẢO]
                {context}

                [CÂU HỎI]
                {question}'
                WHERE Id = 1
                  AND PromptTemplate LIKE N'%QUY TẮC NGÔN NGỮ (BẮT BUỘC, không có ngoại lệ)%';
                """);
        }
    }
}
