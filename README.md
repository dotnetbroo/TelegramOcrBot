# Telegram OCR Bot

A Telegram bot built with C# and Tesseract OCR for extracting text from images.

## Features

* **Image to Text Conversion:** Automatically recognizes text from images sent to the bot.
* **Multi-Language Support:** Supports a wide range of languages. Currently configured for Uzbek (Latin), English, Russian, and Uzbek (Cyrillic), but can easily use any of the 133 languages already included in the `tessdata` folder.
* **Formatted Output:** Sends the recognized text back in a readable, pre-formatted (code block) style within Telegram.

## Technologies Used

* C# (.NET 9.0)
* Telegram.Bot Library
* Tesseract OCR Engine
* Tesseract.NET Wrapper
  
## Usage
Once the bot is running, open a chat with it on Telegram and send an image. The bot will process the image and send back the recognized text.

<img width="2518" height="2032" alt="image" src="https://github.com/user-attachments/assets/e2199342-d895-4a5c-a991-c58e97cb39dd" />

## Contribution
Feel free to fork the repository, make improvements, and submit pull requests.
