using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CSSCADTools.Utilities 
{
    public static class TextParser 
    {
        public static List<string> ParseDetailNumbers(string textContent) 
        {
            List<string> details = new List<string>();
            if (string.IsNullOrWhiteSpace(textContent)) return details;

            // ==========================================
            // 1. DỌN RÁC MTEXT (CASE-SENSITIVE STRIP)
            // ==========================================
            string text = textContent;
            
            // 1.1 Khử Unicode (\U+XXXX)
            text = Regex.Replace(text, @"\\U\+[A-Fa-f0-9]{4}", " ");
            
            // 1.2 ĐƯA REGEX NÀY LÊN TRƯỚC: Khử mã định dạng phức tạp có dấu chấm phẩy 
            // (Ví dụ: \fArial|b0;, \pxqc;, \C1;...)
            text = Regex.Replace(text, @"\\[^;\\\n]*?;", "");
            
            // 1.3 SAU ĐÓ MỚI ÉP PHẲNG: \P và \N là xuống dòng. 
            // TUYỆT ĐỐI KHÔNG replace "\\p" vì nó là mã định dạng paragraph đã được gỡ ở bước 1.2
            text = text.Replace("\\P", " ").Replace("\\N", " ");
            
            // 1.4 Khử các mã định dạng đơn bật/tắt (\L, \O, \K, \X, \~)
            text = Regex.Replace(text, @"\\[LlOoKkxX~]", "");
            
            // 1.5 Khử ngoặc nhọn lồng nhau
            text = Regex.Replace(text, @"[{}]", "");

            // 1.6 Loại bỏ nội dung trong ngoặc đơn — đây là annotation, không phải Detail ID
            // VD: "DET.V91(T13-T13)" → "DET.V91"
            text = Regex.Replace(text, @"\([^)]*\)", "");

            // 1.7 Loại bỏ tỷ lệ (scale) dạng X:Y — tránh ghép nhầm vào Detail ID
            // VD: "DET.B 1:2.5" → "DET.B" (không ghép "125" từ "1:2.5")
            text = Regex.Replace(text, @"\d+\s*:\s*\d+(\.\d+)?", " ");
            
            // ==========================================
            // 2. ÉP PHẲNG DỮ LIỆU (FLATTENING)
            // ==========================================
            string cleaned = text.ToUpper();
            
            // Biến tất cả các phím Enter (xuống dòng thực tế) thành dấu cách
            cleaned = cleaned.Replace("\n", " ").Replace("\r", " ");
            
            // Danh sách Stop-words để chặn đuôi
            string[] stopWords = { "SCALE", "TYP", "MIRROR", "IMAGE", "PLATE", "TOP", "BOTTOM", "THK", "DWG", "REF", "SIM", "OF", "FOR", "POSITION", "PANEL", "SIDE", "BETWEEN" };

            // ==========================================
            // 3. THUẬT TOÁN BÓC TÁCH & GHÉP MẢNH (FUSING)
            // ==========================================
            string[] parts = cleaned.Split(new[] { "DET", "DETAIL" }, StringSplitOptions.None);
            
            for (int i = 1; i < parts.Length; i++) 
            {
                string segment = parts[i].Replace(" AND ", " & ");
                string[] subSegs = segment.Split(new[] { ',', '&' }, StringSplitOptions.RemoveEmptyEntries);
                
                foreach (string sub in subSegs) 
                {
                    string[] words = sub.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    string currentId = "";

                    foreach (string w in words) 
                    {
                        string cleanW = Regex.Replace(w, @"[^A-Z0-9-]", "");
                        if (string.IsNullOrEmpty(cleanW)) continue;
                        
                        if (stopWords.Contains(cleanW)) break;

                        if (cleanW.Any(char.IsDigit)) 
                        {
                            currentId += cleanW;
                            if (cleanW.Any(char.IsLetter)) break; 
                        } 
                        else if (cleanW.Length <= 2) 
                        {
                            currentId += cleanW;
                        } 
                        else 
                        {
                            break;
                        }
                    }

                    currentId = currentId.Trim('-');

                    if (!string.IsNullOrEmpty(currentId) && currentId.Any(char.IsDigit)) 
                    {
                        if (!details.Contains(currentId)) 
                        {
                            details.Add(currentId);
                        }
                    }
                }
            }
            
            return details;
        }
    }
}