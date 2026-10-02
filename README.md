# SmartVideoOptimizer ⚡

<p align="center">
  <img src="https://img.shields.io/badge/.NET-10.0%20LTS-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET 10" />
  <img src="https://img.shields.io/badge/C%23-14.0-239120?style=for-the-badge&logo=c-sharp&logoColor=white" alt="C#" />
  <img src="https://img.shields.io/badge/Platform-Windows%2010%2B-0078D6?style=for-the-badge&logo=windows&logoColor=white" alt="Windows" />
  <img src="https://img.shields.io/badge/Engine-FFmpeg%20%26%20ffprobe-007808?style=for-the-badge&logo=ffmpeg&logoColor=white" alt="FFmpeg" />
  <img src="https://img.shields.io/badge/License-MIT-green?style=for-the-badge" alt="License" />
</p>

<p align="center">
  <strong>🎬 Precision Video Compression Studio — Target exact file sizes, analyze streams, and encode with zero quality guesswork.</strong>
</p>

<p align="center">
  <a href="#-key-features">Key Features</a> •
  <a href="#-architecture">Architecture</a> •
  <a href="#-comparison-table">Comparison</a> •
  <a href="#-getting-started">Getting Started</a> •
  <a href="#-راهنمای-فارسی">راهنمای فارسی</a>
</p>

---

## 🌟 Key Features

- 🎯 **Decoupled Intent-Driven Architecture:** UI only expresses compression intent (`JobRequest`); the engine dynamically designs the optimal `EncodingPlan`. Zero command-line guesswork.
- 🔍 **Deep Media Probing:** Instant stream breakdown powered by native `ffprobe`. Inspects video codecs, frame rates, 10-bit depth, HDR (HDR10, HDR10+, Dolby Vision, HLG), rotation, and audio/subtitle tracks.
- 📦 **Preservation of Complex Containers:** Full multi-track audio preservation, multi-language subtitles (including bitmap PGS subtitles), chapters, and color metadata.
- 🎨 **Glassmorphic Cyber-Dark UI:** Built on WPF and .NET 10 LTS with hardware-accelerated rendering and fluid animations.
- 🌐 **Pure Bilingual Experience:** Seamless one-click toggle between English (LTR) and Pure Persian / فارسی (RTL).
- 🛡️ **Zero-Friction Distribution:** Completely self-contained; no Python or external runtime installation required.

---

## 🏗️ Architecture

```
USER INTERACTION
       │
       ▼
┌──────────────────────────────┐
│   SmartVideoOptimizer.App    │  WPF + MVVM Interface
└──────────────┬───────────────┘
               │ Emits JobRequest (User Intent)
               ▼
┌──────────────────────────────┐
│   SmartVideoOptimizer.Core   │  Domain Models, Planners & Interfaces
└──────────────┬───────────────┘
               │
       ┌───────┴───────┐
       ▼               ▼
┌──────────────┐┌──────────────┐
│    .Media    ││  .Platform   │
│   ffprobe    ││ Windows GPU  │
│  Fast Probe  ││ & Storage    │
└──────────────┘└──────────────┘
```

### Clean Project Separation
1. **`SmartVideoOptimizer.App`:** WPF Views, ViewModels, and modern themes.
2. **`SmartVideoOptimizer.Core`:** Pure domain models (`MediaAsset`, `VideoStream`, `AudioStream`, `SubtitleStream`, `JobRequest`).
3. **`SmartVideoOptimizer.Media`:** Native `ffprobe` process runner, DTOs, and high-performance JSON parsing.
4. **`SmartVideoOptimizer.Platform.Windows`:** Windows file system safety, available disk space validator, and hardware capabilities.
5. **`SmartVideoOptimizer.Tests`:** Automated xUnit test suite validating JSON parsing and live probe executions.

---

## 📊 Comparison Table

| Feature / Capability | SmartVideoOptimizer ⚡ | HandBrake | Traditional FFmpeg GUI | Web Compressors |
| :--- | :---: | :---: | :---: | :---: |
| **Exact Target Size Engine** | ✅ Never-Exceed Mode | ⚠️ Bitrate trial-and-error | ❌ Manual calculation | ⚠️ Lossy & Cloud only |
| **UI Decoupled from CLI** | ✅ Intent-based | ❌ Complex encoder UI | ❌ Exposes raw flags | ⚠️ Black-box |
| **Full HDR & 10-Bit Detection** | ✅ Auto HDR10 / DV / HLG | ⚠️ Partial | ❌ Manual check | ❌ Stripped |
| **Multi-Track Audio & PGS** | ✅ Full container map | ✅ Supported | ⚠️ Error-prone | ❌ Usually 1 track |
| **Local-First & Offline** | ✅ 100% Local | ✅ Local | ✅ Local | ❌ Requires upload |
| **Modern Dark Desktop UI** | ✅ Cyber Slate (.NET 10) | ⚠️ Classic WinForms | ⚠️ Outdated | ⚠️ Browser based |
| **Bilingual (English & Persian)** | ✅ Pure RTL / LTR | ❌ English only | ❌ English only | ❌ Rare |

---

## 🚀 Getting Started

### Prerequisites
- Windows 10 (1809+) or Windows 11 (64-bit)
- [.NET 10 SDK / Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (for compiling from source)

### Running from Source
```bash
# Clone the repository
git clone https://github.com/sadraahkami/smart-video-optimizer.git
cd smart-video-optimizer

# Build and launch
dotnet build
dotnet run --project src/SmartVideoOptimizer.App
```

### Running Tests
```bash
dotnet test
```

---

## 🇮🇷 راهنمای فارسی

### بهینه‌ساز هوشمند ویدیو (SmartVideoOptimizer)
این نرم‌افزار یک استودیوی پیشرفته و دقیق برای تحلیل، پردازش و فشرده‌سازی ویدیو در ویندوز است که با تکیه بر معماری تمیز (Clean Architecture)، دات‌نت ۱۰ (.NET 10 LTS) و موتور FFmpeg توسعه یافته است.

### ویژگی‌های شاخص فاز پایه (نسخه ۰.۱)
- 🎬 **تحلیل سریع و بی‌درنگ:** استخراج کلیه مشخصات ویدیویی، نرخ فریم، ابعاد، و کانتینر با یک Drag & Drop ساده.
- 🌈 **تشخیص هوشمند عمق رنگ و HDR:** شناسایی استریم‌های 10-Bit، دالبی ویژن (Dolby Vision)، HDR10 و HLG.
- 🔊 **پشتیبانی کامل از چند ترک صوتی:** نمایش زبان، کدک و چیدمان کانال‌ها (استریو، سوراند ۵.۱ و ۷.۱).
- 💬 **شناسایی انواع زیرنویس:** پشتیبانی از زیرنویس‌های متنی و تصویری بلوری (PGS).
- 🌐 **رابط کاربری صددرصد فارسی:** بدون به‌هم‌ریختگی چیدمان متون و کاملاً منطبق بر استاندارد راست‌به‌چپ (RTL).

---

## 📄 License
This project is licensed under the [MIT License](LICENSE).
