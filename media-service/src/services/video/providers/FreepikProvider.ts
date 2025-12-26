/**
 * Freepik Provider (Pikaso AI)
 *
 * Provider สำหรับสร้างวีดีโอผ่าน Freepik Pikaso AI
 * ใช้ Web Learning Automation เพื่อเรียนรู้และทำงานอัตโนมัติ
 *
 * @module services/video/providers/FreepikProvider
 */

import { chromium, Browser, Page } from 'playwright';
import { v4 as uuidv4 } from 'uuid';
import * as path from 'path';
import * as fs from 'fs/promises';

import { BaseVideoProvider } from './BaseVideoProvider';
import type {
  VideoGenerationConfig,
  VideoResult,
  VideoProvider,
  JobStatus,
  FreepikConfig,
  VideoMetadata,
} from '@types/video.types';

/**
 * Freepik Provider Class
 * ใช้ Web Learning เพื่อสร้างวีดีโอผ่าน Freepik Pikaso AI
 */
export class FreepikProvider extends BaseVideoProvider {
  protected readonly providerName: VideoProvider = 'freepik' as VideoProvider;
  protected readonly providerUrl: string = 'https://www.freepik.com/pikaso';

  /** Login URL */
  private readonly loginUrl = 'https://www.freepik.com/profile/login';

  /** Pikaso AI URL */
  private readonly pikasoUrl = 'https://www.freepik.com/pikaso/ai-video-generator';

  /** Workflow storage path */
  private readonly workflowPath = './workflows/freepik-workflow.json';

  /** Download directory */
  private readonly downloadDir = './downloads/freepik';

  /** สถานะการเรียนรู้ */
  private isLearning = false;

  /** Learned workflow */
  private learnedWorkflow: any = null;

  /**
   * Initialize provider
   * เตรียม browser และตรวจสอบ session
   */
  async initialize(): Promise<void> {
    this.log('กำลัง initialize Freepik Provider...', 'info');

    // สร้างไดเรกทอรีที่จำเป็น
    await fs.mkdir(this.downloadDir, { recursive: true });
    await fs.mkdir(path.dirname(this.workflowPath), { recursive: true });

    // โหลด workflow ที่เรียนรู้ไว้แล้ว
    await this.loadWorkflow();

    // Launch browser
    this.browser = await chromium.launch({
      headless: process.env.PLAYWRIGHT_HEADLESS === 'true',
      slowMo: parseInt(process.env.PLAYWRIGHT_SLOW_MO || '0'),
      args: [
        '--disable-blink-features=AutomationControlled',
        '--no-sandbox',
        '--disable-setuid-sandbox',
      ],
    });

    // สร้าง context พร้อม session (ถ้ามี)
    const sessionData = await this.loadSession();
    const context = await this.browser.newContext({
      ...(sessionData?.cookies && { storageState: sessionData }),
      userAgent:
        'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36',
      viewport: { width: 1920, height: 1080 },
    });

    this.page = await context.newPage();

    this.log('Freepik Provider initialized successfully', 'info');
  }

  /**
   * ตรวจสอบว่า session ยังใช้ได้หรือไม่
   */
  async isSessionValid(): Promise<boolean> {
    if (!this.page) {
      return false;
    }

    try {
      await this.page.goto(this.pikasoUrl, { waitUntil: 'networkidle' });

      // รอ 2 วินาทีให้หน้าโหลดเสร็จ
      await this.page.waitForTimeout(2000);

      // ตรวจสอบว่ามี login button หรือไม่
      const loginButton = await this.page.$('button:has-text("Log in"), a:has-text("Log in")');

      // ถ้าไม่มี login button แสดงว่า login อยู่แล้ว
      return loginButton === null;
    } catch (error) {
      this.log(`Error checking session: ${error}`, 'error');
      return false;
    }
  }

  /**
   * Login to Freepik
   */
  async login(): Promise<boolean> {
    if (!this.page || !this.credentials) {
      throw new Error('Page or credentials not initialized');
    }

    this.log('กำลัง login เข้า Freepik...', 'info');

    try {
      await this.page.goto(this.loginUrl, { waitUntil: 'networkidle' });

      // รอหน้า login โหลด
      await this.randomDelay(1000, 2000);

      // กรอก email
      const emailSelector = 'input[type="email"], input[name="email"]';
      await this.waitForElement(emailSelector, 10000);
      await this.fillInput(emailSelector, this.credentials.email || '');
      await this.randomDelay(500, 1000);

      // กรอก password
      const passwordSelector = 'input[type="password"], input[name="password"]';
      await this.fillInput(passwordSelector, this.credentials.password || '');
      await this.randomDelay(500, 1000);

      // คลิก login button
      const loginButtonSelector = 'button[type="submit"], button:has-text("Log in")';
      await this.clickElement(loginButtonSelector);

      // รอจนกว่า login สำเร็จ (ดูจาก URL change หรือ element)
      await this.page.waitForURL('**/profile/**', { timeout: 30000 }).catch(() => {});

      // รอให้แน่ใจว่า login สำเร็จแล้ว
      await this.randomDelay(2000, 3000);

      // บันทึก session
      const sessionData = await this.page.context().storageState();
      await this.saveSession(sessionData);

      this.log('Login สำเร็จ!', 'info');

      return true;
    } catch (error) {
      this.log(`Login failed: ${error}`, 'error');
      return false;
    }
  }

  /**
   * Logout from Freepik
   */
  async logout(): Promise<void> {
    if (!this.page) {
      return;
    }

    try {
      // ลบ session file
      await fs.unlink(this.sessionPath).catch(() => {});

      this.log('Logout สำเร็จ', 'info');
    } catch (error) {
      this.log(`Logout error: ${error}`, 'error');
    }
  }

  /**
   * Generate video
   * สร้างวีดีโอโดยใช้ Web Learning
   */
  async generate(config: VideoGenerationConfig): Promise<VideoResult> {
    if (!this.page) {
      throw new Error('Provider not initialized');
    }

    const jobId = uuidv4();
    const startTime = Date.now();

    this.log(`เริ่มสร้างวีดีโอ (Job ID: ${jobId})`, 'info');
    this.log(`Prompt: "${config.prompt}"`, 'info');

    try {
      // ตรวจสอบ session
      const isValid = await this.isSessionValid();
      if (!isValid) {
        this.log('Session หมดอายุ กำลัง login ใหม่...', 'warn');
        const loginSuccess = await this.login();
        if (!loginSuccess) {
          throw new Error('Login failed');
        }
      }

      // ไปหน้า Pikaso AI Video Generator
      await this.page.goto(this.pikasoUrl, { waitUntil: 'networkidle' });
      await this.randomDelay(2000, 3000);

      // ถ้ามี workflow ที่เรียนรู้ไว้แล้ว ใช้ workflow นั้น
      // ถ้ายังไม่มี ให้เข้า learning mode
      let videoUrl: string;

      if (this.learnedWorkflow) {
        this.log('ใช้ learned workflow ในการสร้างวีดีโอ', 'info');
        videoUrl = await this.executeLearnedWorkflow(config);
      } else {
        this.log('ยังไม่มี workflow ที่เรียนรู้ จะเริ่ม learning mode', 'warn');
        videoUrl = await this.learnAndExecute(config);
      }

      // Download วีดีโอ
      const outputFilename = `${jobId}.mp4`;
      const outputPath = path.join(this.downloadDir, outputFilename);
      const videoPath = await this.downloadVideo(videoUrl, outputPath);

      // สร้าง metadata
      const metadata = await this.extractMetadata(videoPath, config);

      const processingTime = (Date.now() - startTime) / 1000;

      this.log(`สร้างวีดีโอสำเร็จ! ใช้เวลา ${processingTime.toFixed(2)} วินาที`, 'info');

      return {
        success: true,
        jobId,
        videoUrl,
        videoPath,
        metadata,
        processingTime,
      };
    } catch (error) {
      this.log(`Error generating video: ${error}`, 'error');

      // บันทึก screenshot เมื่อเกิด error
      await this.takeScreenshot(`error-${jobId}.png`).catch(() => {});

      return {
        success: false,
        jobId,
        metadata: {} as VideoMetadata,
        error: error instanceof Error ? error.message : String(error),
      };
    }
  }

  /**
   * Execute learned workflow
   * ใช้ workflow ที่เรียนรู้ไว้แล้วในการสร้างวีดีโอ
   */
  private async executeLearnedWorkflow(config: VideoGenerationConfig): Promise<string> {
    if (!this.page || !this.learnedWorkflow) {
      throw new Error('Page or workflow not initialized');
    }

    this.log('กำลัง execute learned workflow...', 'info');

    const { steps } = this.learnedWorkflow;

    for (const [index, step] of steps.entries()) {
      this.log(`Step ${index + 1}/${steps.length}: ${step.action}`, 'info');

      try {
        switch (step.action) {
          case 'click':
            await this.executeClick(step);
            break;

          case 'fill':
            await this.executeFill(step, config);
            break;

          case 'select':
            await this.executeSelect(step, config);
            break;

          case 'wait':
            await this.executeWait(step);
            break;

          case 'screenshot':
            await this.takeScreenshot(`step-${index + 1}.png`);
            break;

          default:
            this.log(`Unknown action: ${step.action}`, 'warn');
        }

        // หน่วงเวลาเล็กน้อยระหว่างแต่ละ step
        await this.randomDelay(800, 1500);
      } catch (error) {
        this.log(`Error in step ${index + 1}: ${error}`, 'error');

        // พยายาม self-heal ถ้า element ไม่พบ
        const healed = await this.attemptSelfHeal(step);
        if (!healed) {
          throw error;
        }
      }
    }

    // รอวีดีโอสร้างเสร็จ
    const videoUrl = await this.waitForVideoGeneration();

    return videoUrl;
  }

  /**
   * Learn and execute
   * เรียนรู้ workflow ใหม่และ execute
   */
  private async learnAndExecute(config: VideoGenerationConfig): Promise<string> {
    if (!this.page) {
      throw new Error('Page not initialized');
    }

    this.isLearning = true;
    this.log('เข้าสู่ Learning Mode', 'info');

    const workflow: any = {
      version: '1.0',
      provider: 'freepik',
      createdAt: new Date().toISOString(),
      steps: [],
    };

    try {
      // Step 1: หา prompt input
      this.log('[Learning] กำลังค้นหา prompt input...', 'info');
      const promptSelector = await this.findPromptInput();
      workflow.steps.push({
        action: 'fill',
        selector: promptSelector,
        field: 'prompt',
        description: 'กรอก video prompt',
      });

      // กรอก prompt
      await this.page.fill(promptSelector, config.prompt);
      await this.randomDelay();

      // Step 2: หา generate button
      this.log('[Learning] กำลังค้นหา generate button...', 'info');
      const generateButtonSelector = await this.findGenerateButton();
      workflow.steps.push({
        action: 'click',
        selector: generateButtonSelector,
        description: 'คลิกปุ่ม generate',
      });

      // คลิก generate
      await this.page.click(generateButtonSelector);

      // Step 3: รอผลลัพธ์
      workflow.steps.push({
        action: 'wait',
        selector: await this.findVideoResultSelector(),
        timeout: 180000, // 3 นาที
        description: 'รอวีดีโอสร้างเสร็จ',
      });

      // Step 4: หา download button
      this.log('[Learning] รอวีดีโอสร้างเสร็จ...', 'info');
      const downloadSelector = await this.waitForDownloadButton();
      workflow.steps.push({
        action: 'click',
        selector: downloadSelector,
        description: 'คลิกดาวน์โหลด',
      });

      // ดึง video URL
      const videoUrl = await this.extractVideoUrl();

      // บันทึก workflow ที่เรียนรู้
      await this.saveWorkflow(workflow);
      this.learnedWorkflow = workflow;

      this.log('[Learning] เรียนรู้ workflow สำเร็จ!', 'info');
      this.isLearning = false;

      return videoUrl;
    } catch (error) {
      this.isLearning = false;
      throw error;
    }
  }

  /**
   * Find prompt input
   * ค้นหา input field สำหรับ prompt
   */
  private async findPromptInput(): Promise<string> {
    if (!this.page) {
      throw new Error('Page not initialized');
    }

    // ลองหลายๆ selector
    const possibleSelectors = [
      'textarea[placeholder*="prompt" i]',
      'textarea[placeholder*="describe" i]',
      'input[type="text"][placeholder*="prompt" i]',
      'div[contenteditable="true"]',
      '[data-testid*="prompt"]',
      '[aria-label*="prompt" i]',
    ];

    for (const selector of possibleSelectors) {
      const element = await this.page.$(selector);
      if (element) {
        this.log(`พบ prompt input: ${selector}`, 'info');
        return selector;
      }
    }

    throw new Error('ไม่พบ prompt input field');
  }

  /**
   * Find generate button
   * ค้นหาปุ่ม generate
   */
  private async findGenerateButton(): Promise<string> {
    if (!this.page) {
      throw new Error('Page not initialized');
    }

    const possibleSelectors = [
      'button:has-text("Generate")',
      'button:has-text("Create")',
      'button:has-text("สร้าง")',
      '[data-testid*="generate"]',
      '[aria-label*="generate" i]',
    ];

    for (const selector of possibleSelectors) {
      const element = await this.page.$(selector);
      if (element) {
        this.log(`พบ generate button: ${selector}`, 'info');
        return selector;
      }
    }

    throw new Error('ไม่พบ generate button');
  }

  /**
   * Find video result selector
   * ค้นหา selector ของผลลัพธ์วีดีโอ
   */
  private async findVideoResultSelector(): Promise<string> {
    // ใช้ selector ทั่วไปสำหรับวีดีโอ
    return 'video, [data-testid*="video"], [class*="video-result"]';
  }

  /**
   * Wait for download button
   * รอปุ่มดาวน์โหลดปรากฏ
   */
  private async waitForDownloadButton(): Promise<string> {
    if (!this.page) {
      throw new Error('Page not initialized');
    }

    const possibleSelectors = [
      'button:has-text("Download")',
      'a:has-text("Download")',
      '[data-testid*="download"]',
      '[aria-label*="download" i]',
    ];

    for (const selector of possibleSelectors) {
      const found = await this.waitForElement(selector, 180000); // รอ 3 นาที
      if (found) {
        this.log(`พบ download button: ${selector}`, 'info');
        return selector;
      }
    }

    throw new Error('ไม่พบ download button');
  }

  /**
   * Extract video URL
   * ดึง URL ของวีดีโอ
   */
  private async extractVideoUrl(): Promise<string> {
    if (!this.page) {
      throw new Error('Page not initialized');
    }

    // ลอง extract จาก video element
    const videoElement = await this.page.$('video');
    if (videoElement) {
      const src = await videoElement.getAttribute('src');
      if (src) {
        return src;
      }
    }

    // ลอง extract จาก download link
    const downloadLink = await this.page.$('a[download], a[href*=".mp4"]');
    if (downloadLink) {
      const href = await downloadLink.getAttribute('href');
      if (href) {
        return href;
      }
    }

    throw new Error('ไม่สามารถดึง video URL ได้');
  }

  /**
   * Wait for video generation
   * รอจนกว่าวีดีโอจะสร้างเสร็จ
   */
  private async waitForVideoGeneration(): Promise<string> {
    if (!this.page) {
      throw new Error('Page not initialized');
    }

    this.log('กำลังรอวีดีโอสร้างเสร็จ...', 'info');

    // รอ video element หรือ download button
    await this.page.waitForSelector('video, button:has-text("Download")', {
      timeout: 300000, // รอ 5 นาที
    });

    // ดึง video URL
    return await this.extractVideoUrl();
  }

  /**
   * Download video
   * ดาวน์โหลดวีดีโอจาก URL
   */
  async downloadVideo(url: string, outputPath: string): Promise<string> {
    this.log(`กำลังดาวน์โหลดวีดีโอ: ${url}`, 'info');

    const axios = (await import('axios')).default;

    const response = await axios.get(url, {
      responseType: 'arraybuffer',
    });

    await fs.writeFile(outputPath, response.data);

    this.log(`ดาวน์โหลดเสร็จสิ้น: ${outputPath}`, 'info');

    return outputPath;
  }

  /**
   * Get job status
   * (ไม่ใช้สำหรับ Freepik เนื่องจากเป็น synchronous)
   */
  async getJobStatus(jobId: string): Promise<JobStatus> {
    // Freepik ไม่มี async job system
    return 'completed' as JobStatus;
  }

  /**
   * Wait for completion
   * (ไม่ใช้สำหรับ Freepik เนื่องจากเป็น synchronous)
   */
  async waitForCompletion(jobId: string, timeout?: number): Promise<VideoResult> {
    throw new Error('Method not implemented for FreepikProvider');
  }

  /**
   * Execute click action
   */
  private async executeClick(step: any): Promise<void> {
    if (!this.page) throw new Error('Page not initialized');
    await this.page.click(step.selector);
  }

  /**
   * Execute fill action
   */
  private async executeFill(step: any, config: VideoGenerationConfig): Promise<void> {
    if (!this.page) throw new Error('Page not initialized');

    let value = '';

    switch (step.field) {
      case 'prompt':
        value = config.prompt;
        break;
      default:
        value = step.value || '';
    }

    await this.page.fill(step.selector, value);
  }

  /**
   * Execute select action
   */
  private async executeSelect(step: any, config: VideoGenerationConfig): Promise<void> {
    if (!this.page) throw new Error('Page not initialized');
    await this.page.selectOption(step.selector, step.value);
  }

  /**
   * Execute wait action
   */
  private async executeWait(step: any): Promise<void> {
    if (!this.page) throw new Error('Page not initialized');
    await this.page.waitForSelector(step.selector, { timeout: step.timeout || 30000 });
  }

  /**
   * Attempt self-heal
   * พยายาม heal workflow เมื่อ element ไม่พบ
   */
  private async attemptSelfHeal(step: any): Promise<boolean> {
    this.log('พยายาม self-heal workflow...', 'warn');

    // TODO: Implement AI-based element finding
    // ใช้ AI ในการหา element ที่คล้ายกัน

    return false;
  }

  /**
   * Extract metadata from video file
   */
  private async extractMetadata(
    videoPath: string,
    config: VideoGenerationConfig
  ): Promise<VideoMetadata> {
    const ffmpeg = (await import('fluent-ffmpeg')).default;
    const stats = await fs.stat(videoPath);

    return new Promise((resolve, reject) => {
      ffmpeg.ffprobe(videoPath, (err, metadata) => {
        if (err) {
          reject(err);
          return;
        }

        const videoStream = metadata.streams.find((s) => s.codec_type === 'video');

        resolve({
          provider: this.providerName,
          duration: metadata.format.duration || config.duration,
          width: videoStream?.width || 1920,
          height: videoStream?.height || 1080,
          aspectRatio: config.aspectRatio || '16:9',
          fps: eval(videoStream?.r_frame_rate || '30') || 30,
          fileSize: stats.size,
          format: metadata.format.format_name || 'mp4',
          codec: videoStream?.codec_name,
          bitrate: metadata.format.bit_rate,
          prompt: config.prompt,
          negativePrompt: config.negativePrompt,
          seed: config.seed,
          createdAt: new Date(),
        });
      });
    });
  }

  /**
   * Load workflow from file
   */
  private async loadWorkflow(): Promise<void> {
    try {
      const data = await fs.readFile(this.workflowPath, 'utf-8');
      this.learnedWorkflow = JSON.parse(data);
      this.log('Loaded learned workflow successfully', 'info');
    } catch (error) {
      this.log('No learned workflow found', 'info');
    }
  }

  /**
   * Save workflow to file
   */
  private async saveWorkflow(workflow: any): Promise<void> {
    await fs.writeFile(this.workflowPath, JSON.stringify(workflow, null, 2), 'utf-8');
    this.log('Saved learned workflow', 'info');
  }
}
