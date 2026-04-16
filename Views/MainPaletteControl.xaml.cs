using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Autodesk.AutoCAD.ApplicationServices;
using Application = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using CSSCADTools.Utilities;
using CSSCADTools.Models;
using System.Collections.Generic;

namespace CSSCADTools.Views
{
    public partial class MainPaletteControl : UserControl
    {
        public MainPaletteControl()
        {
            // Khởi tạo các thành phần giao diện đã định nghĩa trong XAML
            InitializeComponent();
        }

        /// <summary>
        /// Chức năng 1: Kiểm tra Detail và xuất báo cáo (Summary + Raw Data Log)
        /// </summary>
        private async void BtnCheckDetails_Click(object sender, RoutedEventArgs e)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            // 1. Chọn thư mục DWG (Windows Explorer-style dialog)
            string folderPath = FolderPicker.Show("Chọn thư mục chứa các bản vẽ DWG cần kiểm tra");
            if (string.IsNullOrEmpty(folderPath)) return;

            string[] dwgFiles = Directory.GetFiles(folderPath, "*.dwg", SearchOption.TopDirectoryOnly);
            if (dwgFiles.Length == 0)
            {
                MessageBox.Show("Không tìm thấy file .dwg nào trong thư mục!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Chọn nơi lưu file báo cáo CSV
            string reportPath = "";
            using (var saveDialog = new System.Windows.Forms.SaveFileDialog())
            {
                saveDialog.Filter = "CSV File|*.csv";
                saveDialog.Title = "Lưu báo cáo kiểm tra";
                saveDialog.FileName = $"Report_{DateTime.Now:yyyyMMdd_HHmm}.csv";
                if (saveDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                reportPath = saveDialog.FileName;
            }

            // 3. Cập nhật trạng thái UI
            SetUIState(false);
            TxtStatus.Text = "Status: Scanning & Processing...";

            try
            {
                // 4. Chạy xử lý nặng ở luồng ngầm
                await Task.Run(() =>
                {
                    var scan = DwgDatabase.ScanFiles(dwgFiles);
                    FileExporter.ExportReport(scan, reportPath);
                    FileExporter.ExportDataLog(scan, reportPath);
                });

                string baseName = Path.GetFileNameWithoutExtension(reportPath);
                TxtStatus.Text = "Status: Finished!";
                MessageBox.Show(
                    $"Hoàn tất!\n\n" +
                    $"1. {baseName}_DETAIL.csv\n" +
                    $"2. {baseName}_SECTION.csv\n" +
                    $"3. {baseName}_DATALOG.csv",
                    "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "Status: Error occurred!";
                MessageBox.Show($"Lỗi trong quá trình xử lý:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetUIState(true);
            }
        }

        /// <summary>
        /// Chức năng 2: Tính tổng chiều dài Line (từ VBA)
        /// </summary>
        private void BtnTotalLength_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TxtStatus.Text = "Status: Select lines...";
                LineUtils.CalculateTotalLength();
                TxtStatus.Text = "Status: Ready";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}");
                TxtStatus.Text = "Status: Ready";
            }
        }

        /// <summary>
        /// Chức năng 3: Tính điểm trung bình (từ VBA - GeometryUtils)
        /// </summary>
        private void BtnAverageMidpoint_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TxtStatus.Text = "Status: Select objects...";
                GeometryUtils.CalculateAverageMidpoint();
                TxtStatus.Text = "Status: Ready";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}");
                TxtStatus.Text = "Status: Ready";
            }
        }

        /// <summary>
        /// Chức năng 4: Đổi tên Block ngẫu nhiên (từ VBA - BlockRenameUtils)
        /// </summary>
        private void BtnRandomRename_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TxtStatus.Text = "Status: Select a block...";
                BlockRenameUtils.RandomRenameBlock();
                TxtStatus.Text = "Status: Ready";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}");
                TxtStatus.Text = "Status: Ready";
            }
        }

        /// <summary>
        /// Quản lý trạng thái các nút bấm khi Tool đang bận
        /// </summary>
        private void SetUIState(bool isEnabled)
        {
            BtnCheckDetails.IsEnabled = isEnabled;
            BtnTotalLength.IsEnabled = isEnabled;
            BtnAverageMidpoint.IsEnabled = isEnabled;
            BtnRandomRename.IsEnabled = isEnabled;
            
            BtnCheckDetails.Content = isEnabled ? "CHECK DETAILS" : "PROCESSING...";
        }
    }
}