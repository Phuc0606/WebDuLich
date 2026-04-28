using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using WebDuLichDaLat.Models;
using WebDuLichDaLat.Controllers; // ✅ Thêm để sử dụng BudgetPlanType enum

namespace WebDuLichDaLat.Services
{
    /// <summary>
    /// Service chịu trách nhiệm format và render suggestion từ SuggestionViewModel
    /// Tách logic hiển thị HTML ra khỏi Controller
    /// </summary>
    public class SuggestionFormatterService
    {
        /// <summary>
        /// Format currency
        /// </summary>
        private string FormatCurrency(decimal value) => $"{value:N0}đ";

        /// <summary>
        /// Tạo SuggestionViewModel từ các tham số (thay thế cho FormatOptimizedSuggestion)
        /// </summary>
        public SuggestionViewModel CreateSuggestionViewModel(
            TransportOption transport,
            decimal transportCost,
            string hotelDetails,
            decimal hotelCost,
            string foodDetails,
            decimal foodCost,
            decimal ticketCost,
            decimal localTransportCost,
            decimal miscCost,
            decimal totalCost,
            decimal remaining,
            int days,
            List<string> ticketDetails,
            List<string> localTransportDetails,
            List<string> warnings,
            string routeDetails,
            string clusterDetails,
            bool basicOnly,
            List<DailyItinerary> dailyItinerary = null,
            bool isTransportPriceCalculated = false,
            bool isMergedLocation = false,
            string oldLocationName = null,
            string locationName = null)
        {
            var viewModel = new SuggestionViewModel
            {
                Transport = transport,
                TransportCost = transportCost,
                HotelCost = hotelCost,
                FoodCost = foodCost,
                TicketCost = ticketCost,
                LocalTransportCost = localTransportCost,
                MiscCost = miscCost,
                TotalCost = totalCost,
                Remaining = remaining,
                Days = days,
                HotelDetails = hotelDetails,
                FoodDetails = foodDetails,
                TicketDetails = ticketDetails ?? new List<string>(),
                LocalTransportDetails = localTransportDetails ?? new List<string>(),
                Warnings = warnings ?? new List<string>(),
                RouteDetails = routeDetails,
                ClusterDetails = clusterDetails,
                BasicOnly = basicOnly,
                DailyItinerary = dailyItinerary ?? new List<DailyItinerary>(),
                IsTransportPriceCalculated = isTransportPriceCalculated,
                IsMergedLocation = isMergedLocation,
                OldLocationName = oldLocationName,
                LocationName = locationName
            };

            // Tính toán thống kê
            if (dailyItinerary != null && dailyItinerary.Any())
            {
                viewModel.TotalPlaces = dailyItinerary
                    .SelectMany(d => d.Places)
                    .Select(p => p.Id)
                    .Distinct()
                    .Count();

                viewModel.UniquePlaces = dailyItinerary
                    .SelectMany(d => d.Places)
                    .GroupBy(p => p.Id)
                    .Select(g => g.First())
                    .OrderBy(p => p.Name)
                    .ToList();
            }

            // Parse tổng km từ localTransportDetails
            if (localTransportDetails != null && localTransportDetails.Any())
            {
                foreach (var detail in localTransportDetails)
                {
                    var kmMatch = System.Text.RegularExpressions.Regex.Match(
                        detail, 
                        @"Tổng quãng đường:\s*([\d,]+)\s*km", 
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (kmMatch.Success)
                    {
                        var kmStr = kmMatch.Groups[1].Value.Replace(",", "").Replace(".", "");
                        if (decimal.TryParse(kmStr, out var km))
                        {
                            viewModel.TotalKm += km;
                        }
                    }
                }
            }

            return viewModel;
        }

        /// <summary>
        /// Tạo SuggestionViewModel từ các tham số theo BudgetPlan (thay thế cho FormatOptimizedSuggestionByBudgetPlan)
        /// </summary>
        public SuggestionViewModel CreateSuggestionViewModelByBudgetPlan(
            string planName,
            BudgetPlanType? budgetPlanType,
            TransportOption transport,
            decimal transportCost,
            string hotelDetails,
            decimal hotelCost,
            string foodDetails,
            decimal foodCost,
            decimal ticketCost,
            decimal localTransportCost,
            decimal miscCost,
            decimal totalCost,
            decimal remaining,
            int days,
            List<string> ticketDetails,
            List<string> localTransportDetails,
            List<string> warnings,
            string routeDetails,
            string clusterDetails,
            bool basicOnly,
            List<DailyItinerary> dailyItinerary = null,
            bool isTransportPriceCalculated = false,
            bool isMergedLocation = false,
            string oldLocationName = null,
            string locationName = null)
        {
            var viewModel = CreateSuggestionViewModel(
                transport, transportCost, hotelDetails, hotelCost,
                foodDetails, foodCost, ticketCost, localTransportCost,
                miscCost, totalCost, remaining, days, ticketDetails,
                localTransportDetails, warnings, routeDetails, clusterDetails,
                basicOnly, dailyItinerary, isTransportPriceCalculated,
                isMergedLocation, oldLocationName, locationName);

            viewModel.PlanName = planName;
            viewModel.BudgetPlanType = budgetPlanType;

            return viewModel;
        }

        /// <summary>
        /// Render suggestion thành HTML string (tạm thời giữ lại để backward compatible)
        /// TODO: Sau này sẽ render bằng Partial View thay vì StringBuilder
        /// </summary>
        public string RenderSuggestionHtml(SuggestionViewModel model)
        {
            var sb = new StringBuilder();
            var contentId = Guid.NewGuid().ToString("N").Substring(0, 8);

            // Thông báo về giá vận chuyển ước tính
            if (model.IsTransportPriceCalculated)
            {
                sb.Append("<div class='alert alert-warning' style='padding:8px; margin-bottom:10px; background-color:#fff3cd; border:1px solid #ffc107; border-radius:4px;'>");
                sb.Append("⚠️ <strong>Lưu ý:</strong> Giá vận chuyển được tính ước lượng theo khoảng cách GPS. ");
                sb.Append("Vui lòng liên hệ nhà xe để biết giá chính xác.");
                sb.Append("</div>");
            }

            // Thông tin sáp nhập tỉnh
            if (model.IsMergedLocation && !string.IsNullOrEmpty(model.OldLocationName))
            {
                sb.Append("<div class='alert alert-info' style='padding:8px; margin-bottom:10px; background-color:#d1ecf1; border:1px solid #17a2b8; border-radius:4px;'>");
                sb.Append($"🏙️ <strong>Thông tin:</strong> ");
                sb.Append($"{model.OldLocationName} đã sáp nhập vào {model.LocationName} từ 01/07/2025. ");
                sb.Append($"Giá vận chuyển từ khu vực {model.OldLocationName} cũ.");
                sb.Append("</div>");
            }

            // Hiển thị chi phí cơ bản
            sb.Append($"🚗 <strong>{model.Transport.Name}</strong> ({model.Transport.Type}): {FormatCurrency(model.TransportCost)}<br/>");

            if (!model.BasicOnly)
            {
                sb.Append($"🏨 {model.HotelDetails}<br/>");
                sb.Append($"🍽️ {model.FoodDetails}<br/>");
                sb.Append($"🎫 Vé tham quan: {FormatCurrency(model.TicketCost)}<br/>");
            }

            if (!model.BasicOnly && model.LocalTransportCost > 0)
            {
                sb.Append($"🚌 Di chuyển nội thành: {FormatCurrency(model.LocalTransportCost)}<br/>");
                if (model.LocalTransportDetails.Any())
                {
                    foreach (var detail in model.LocalTransportDetails)
                    {
                        sb.Append($"  {detail}<br/>");
                    }
                }
            }

            if (!model.BasicOnly)
            {
                sb.Append($"💡 Chi phí phát sinh (10%): {FormatCurrency(model.MiscCost)}<br/>");
                sb.Append($"💰 <strong>Tổng chi phí: {FormatCurrency(model.TotalCost)} | Còn lại: {FormatCurrency(model.Remaining)}</strong><br/>");
            }

            // Cluster details
            if (!string.IsNullOrEmpty(model.ClusterDetails))
            {
                sb.Append(model.ClusterDetails);
            }

            // Route details
            if (!model.BasicOnly && !string.IsNullOrEmpty(model.RouteDetails))
            {
                sb.Append($"<br/><b>📅 Lịch trình tối ưu:</b><br/>{model.RouteDetails}");
            }

            // Ticket details
            if (!model.BasicOnly && model.TicketDetails.Any())
            {
                sb.Append($"<br/><b>🎫 Chi tiết vé tham quan:</b><br/>");
                foreach (var detail in model.TicketDetails)
                {
                    sb.Append($"{detail}<br/>");
                }
            }

            // Warnings
            if (!model.BasicOnly && model.Warnings.Any())
            {
                sb.Append($"<br/><b>⚠️ Lưu ý:</b><br/>");
                foreach (var w in model.Warnings)
                    sb.Append($"{w}<br/>");
            }

            // Daily itinerary
            if (!model.BasicOnly && model.DailyItinerary != null && model.DailyItinerary.Any())
            {
                sb.Append("<br/><b>📆 Chi tiết từng ngày:</b><br/>");

                var groupedByCluster = model.DailyItinerary.GroupBy(d => d.ClusterIndex);

                foreach (var clusterGroup in groupedByCluster)
                {
                    int clusterIdx = clusterGroup.Key;
                    var daysInCluster = clusterGroup.ToList();

                    var firstDay = daysInCluster.First();
                    string hotelName = firstDay.Hotel?.Name ?? "Chưa chọn KS";

                    sb.Append($"<strong>Giai đoạn {clusterIdx + 1}</strong> " +
                             $"({daysInCluster.Count} ngày tại {hotelName}):<br/>");

                    foreach (var day in daysInCluster)
                    {
                        var placeNames = day.Places.Any()
                            ? string.Join(", ", day.Places.Select(p => p.Name))
                            : "Nghỉ ngơi/tự do";
                        sb.Append($"  • Ngày {day.DayNumber}: {placeNames}<br/>");
                    }
                    sb.Append("<br/>");
                }
            }

            var content = sb.ToString();
            return $"<div class='suggestion' data-transport='{model.TransportCost}' data-hotel='{model.HotelCost}' data-food='{model.FoodCost}' data-ticket='{model.TicketCost}' data-local='{model.LocalTransportCost}' data-misc='{model.MiscCost}' data-total='{model.TotalCost}'>{content}</div>";
        }
    }
}

