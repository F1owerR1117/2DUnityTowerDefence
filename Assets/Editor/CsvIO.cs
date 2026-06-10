using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DoudizhuTower.Editor
{
    /// <summary>
    /// CSV 读写工具类（仅 Editor 使用）。
    /// 支持带引号的字段（含逗号/换行）、UTF-8 BOM。
    /// </summary>
    public static class CsvIO
    {
        /// <summary>
        /// 读取 CSV 文件，返回行列表（每行为 列名→值 字典）。
        /// </summary>
        public static List<Dictionary<string, string>> ReadCsv(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"CSV 文件不存在: {path}");

            var lines = File.ReadAllLines(path, Encoding.UTF8);
            if (lines.Length < 2)
                return new List<Dictionary<string, string>>();

            var headers = ParseLine(lines[0]);
            var result = new List<Dictionary<string, string>>();

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var values = ParseLine(lines[i]);
                var row = new Dictionary<string, string>();
                for (int j = 0; j < headers.Count; j++)
                {
                    row[headers[j]] = j < values.Count ? values[j] : "";
                }
                result.Add(row);
            }

            return result;
        }

        /// <summary>
        /// 将行列表写入 CSV 文件。
        /// </summary>
        public static void WriteCsv(string path, List<string> headers, List<Dictionary<string, string>> rows)
        {
            var sb = new StringBuilder();

            // 写入表头
            sb.AppendLine(string.Join(",", headers.ConvertAll(EscapeField)));

            // 写入数据行
            foreach (var row in rows)
            {
                var fields = new List<string>();
                foreach (var h in headers)
                {
                    row.TryGetValue(h, out var val);
                    fields.Add(EscapeField(val ?? ""));
                }
                sb.AppendLine(string.Join(",", fields));
            }

            // UTF-8 with BOM
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        /// <summary>
        /// 解析单行 CSV，处理引号内的逗号和换行。
        /// </summary>
        private static List<string> ParseLine(string line)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(line)) return result;

            int i = 0;
            while (i <= line.Length)
            {
                if (i == line.Length) { result.Add(""); break; }

                if (line[i] == '"')
                {
                    // 引号字段
                    i++;
                    var sb = new StringBuilder();
                    while (i < line.Length)
                    {
                        if (line[i] == '"')
                        {
                            if (i + 1 < line.Length && line[i + 1] == '"')
                            {
                                sb.Append('"');
                                i += 2;
                            }
                            else
                            {
                                i++;
                                break;
                            }
                        }
                        else
                        {
                            sb.Append(line[i]);
                            i++;
                        }
                    }
                    result.Add(sb.ToString());
                    // 跳过逗号
                    if (i < line.Length && line[i] == ',') i++;
                }
                else
                {
                    // 普通字段
                    int start = i;
                    while (i < line.Length && line[i] != ',') i++;
                    result.Add(line.Substring(start, i - start));
                    if (i < line.Length && line[i] == ',') i++;
                }
            }

            return result;
        }

        /// <summary>
        /// 转义字段（含逗号/引号/换行时加引号）。
        /// </summary>
        private static string EscapeField(string field)
        {
            if (string.IsNullOrEmpty(field)) return "";
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }
            return field;
        }
    }
}
