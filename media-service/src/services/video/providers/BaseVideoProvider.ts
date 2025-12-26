/**
 * Base Video Provider
 *
 * Abstract class สำหรับ Video Providers ทั้งหมด
 * ทุก provider ต้อง extend class นี้และ implement methods ที่จำเป็น
 *
 * @module services/video/providers/BaseVideoProvider
 */

import { Browser, Page } from 'playwright';

import type {
  VideoGenerationConfig,
  VideoResult,
  ProviderCredentials,
  VideoProvider,
  JobStatus,
} from '@types/video.types';

/**
 * Base Video Provider Class
 * คลาสพื้นฐานสำหรับทุก video provider
 */
export abstract class BaseVideoProvider {
  /** Provider name */
  protected abstract readonly providerName: VideoProvider;

  /** Provider URL */
  protected abstract readonly providerUrl: string;

  /** Browser instance */
  protected browser?: Browser;

  /** Page instance */
  protected page?: Page;

  /** Credentials */
  protected credentials?: ProviderCredentials;

  /** Session path */
  protected sessionPath: string;

  /**
   * Constructor
   * @param credentials - Provider credentials
   */
  constructor(credentials?: ProviderCredentials) {
    this.credentials = credentials;
    this.sessionPath = `./sessions/${this.providerName}-session.json`;
  }

  /**
   * Initialize provider
   * เตรียม browser และ login
   */
  abstract initialize(): Promise<void>;

  /**
   * Generate video
   * สร้างวีดีโอตาม config
   *
   * @param config - Video generation configuration
   * @returns Promise<VideoResult>
   */
  abstract generate(config: VideoGenerationConfig): Promise<VideoResult>;

  /**
   * Check if session is valid
   * ตรวจสอบว่า session ยังใช้ได้หรือไม่
   *
   * @returns Promise<boolean>
   */
  abstract isSessionValid(): Promise<boolean>;

  /**
   * Login to provider
   * เข้าสู่ระบบ provider
   *
   * @returns Promise<boolean> - สำเร็จหรือไม่
   */
  abstract login(): Promise<boolean>;

  /**
   * Logout from provider
   * ออกจากระบบ
   */
  abstract logout(): Promise<void>;

  /**
   * Download video
   * ดาวน์โหลดวีดีโอจาก URL
   *
   * @param url - Video URL
   * @param outputPath - Output file path
   * @returns Promise<string> - Path to downloaded file
   */
  abstract downloadVideo(url: string, outputPath: string): Promise<string>;

  /**
   * Get job status
   * ดึงสถานะของ job
   *
   * @param jobId - Job ID
   * @returns Promise<JobStatus>
   */
  abstract getJobStatus(jobId: string): Promise<JobStatus>;

  /**
   * Wait for completion
   * รอจนกว่า job จะเสร็จสิ้น
   *
   * @param jobId - Job ID
   * @param timeout - Timeout in milliseconds
   * @returns Promise<VideoResult>
   */
  abstract waitForCompletion(jobId: string, timeout?: number): Promise<VideoResult>;

  /**
   * Close browser
   * ปิด browser instance
   */
  async closeBrowser(): Promise<void> {
    if (this.page) {
      await this.page.close();
      this.page = undefined;
    }

    if (this.browser) {
      await this.browser.close();
      this.browser = undefined;
    }
  }

  /**
   * Save session
   * บันทึก session ลง file
   *
   * @param sessionData - Session data to save
   */
  protected async saveSession(sessionData: any): Promise<void> {
    const fs = await import('fs/promises');
    const path = await import('path');

    const dir = path.dirname(this.sessionPath);
    await fs.mkdir(dir, { recursive: true });

    await fs.writeFile(this.sessionPath, JSON.stringify(sessionData, null, 2), 'utf-8');
  }

  /**
   * Load session
   * โหลด session จาก file
   *
   * @returns Promise<any | null>
   */
  protected async loadSession(): Promise<any | null> {
    try {
      const fs = await import('fs/promises');
      const data = await fs.readFile(this.sessionPath, 'utf-8');
      return JSON.parse(data);
    } catch (error) {
      return null;
    }
  }

  /**
   * Wait for element
   * รอจนกว่า element จะปรากฏ
   *
   * @param selector - CSS selector
   * @param timeout - Timeout in milliseconds
   * @returns Promise<boolean>
   */
  protected async waitForElement(selector: string, timeout: number = 30000): Promise<boolean> {
    if (!this.page) {
      throw new Error('Page is not initialized');
    }

    try {
      await this.page.waitForSelector(selector, { timeout });
      return true;
    } catch (error) {
      return false;
    }
  }

  /**
   * Click element
   * คลิก element
   *
   * @param selector - CSS selector
   * @returns Promise<boolean>
   */
  protected async clickElement(selector: string): Promise<boolean> {
    if (!this.page) {
      throw new Error('Page is not initialized');
    }

    try {
      await this.page.click(selector);
      return true;
    } catch (error) {
      return false;
    }
  }

  /**
   * Fill input
   * กรอกข้อมูลใน input field
   *
   * @param selector - CSS selector
   * @param value - Value to fill
   * @returns Promise<boolean>
   */
  protected async fillInput(selector: string, value: string): Promise<boolean> {
    if (!this.page) {
      throw new Error('Page is not initialized');
    }

    try {
      await this.page.fill(selector, value);
      return true;
    } catch (error) {
      return false;
    }
  }

  /**
   * Take screenshot
   * บันทึก screenshot
   *
   * @param filename - Output filename
   * @returns Promise<string>
   */
  protected async takeScreenshot(filename: string): Promise<string> {
    if (!this.page) {
      throw new Error('Page is not initialized');
    }

    const fs = await import('fs/promises');
    const path = await import('path');

    const screenshotDir = './screenshots';
    await fs.mkdir(screenshotDir, { recursive: true });

    const screenshotPath = path.join(screenshotDir, filename);
    await this.page.screenshot({ path: screenshotPath, fullPage: true });

    return screenshotPath;
  }

  /**
   * Random delay
   * หน่วงเวลาแบบสุ่มเพื่อให้ดูเหมือนมนุษย์
   *
   * @param min - Minimum delay (ms)
   * @param max - Maximum delay (ms)
   */
  protected async randomDelay(min: number = 1000, max: number = 3000): Promise<void> {
    const delay = Math.floor(Math.random() * (max - min + 1)) + min;
    await new Promise((resolve) => setTimeout(resolve, delay));
  }

  /**
   * Log message
   * บันทึก log message
   *
   * @param message - Log message
   * @param level - Log level
   */
  protected log(message: string, level: 'info' | 'warn' | 'error' = 'info'): void {
    const timestamp = new Date().toISOString();
    const prefix = `[${timestamp}] [${this.providerName.toUpperCase()}]`;

    switch (level) {
      case 'info':
        console.log(`${prefix} ${message}`);
        break;
      case 'warn':
        console.warn(`${prefix} ${message}`);
        break;
      case 'error':
        console.error(`${prefix} ${message}`);
        break;
    }
  }
}
