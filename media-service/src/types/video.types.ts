/**
 * Types สำหรับ Video Generation System
 *
 * ไฟล์นี้ประกอบด้วย type definitions ทั้งหมดที่ใช้ในระบบสร้างวีดีโอ
 * @module types/video.types
 */

/**
 * Video Provider Types
 * แพลตฟอร์มที่รองรับการสร้างวีดีโอ
 */
export enum VideoProvider {
  /** Freepik AI (Pikaso) - ตัวหลัก */
  FREEPIK = 'freepik',
  /** Runway ML - Fallback */
  RUNWAY = 'runway',
  /** Pika Labs - Fallback */
  PIKA = 'pika',
  /** Luma AI - Fallback */
  LUMA = 'luma',
}

/**
 * Video Generation Mode
 * โหมดการสร้างวีดีโอ
 */
export enum VideoGenerationMode {
  /** สร้างจากข้อความ */
  TEXT_TO_VIDEO = 'text-to-video',
  /** สร้างจากรูปภาพ */
  IMAGE_TO_VIDEO = 'image-to-video',
  /** แปลงวีดีโอ */
  VIDEO_TO_VIDEO = 'video-to-video',
  /** ขยาย canvas */
  EXPAND_CANVAS = 'expand-canvas',
}

/**
 * Aspect Ratio Options
 * อัตราส่วนของวีดีโอ
 */
export enum AspectRatio {
  /** 16:9 - YouTube, Landscape */
  LANDSCAPE = '16:9',
  /** 9:16 - TikTok, Reels, Stories */
  PORTRAIT = '9:16',
  /** 1:1 - Instagram Feed */
  SQUARE = '1:1',
  /** 4:3 - Classic */
  CLASSIC = '4:3',
  /** 21:9 - Ultrawide */
  ULTRAWIDE = '21:9',
}

/**
 * Video Quality
 * คุณภาพของวีดีโอ
 */
export enum VideoQuality {
  /** 480p */
  LOW = '480p',
  /** 720p */
  MEDIUM = '720p',
  /** 1080p */
  HIGH = '1080p',
  /** 4K */
  ULTRA = '4k',
}

/**
 * Video Style
 * สไตล์ของวีดีโอ
 */
export enum VideoStyle {
  REALISTIC = 'realistic',
  ANIME = 'anime',
  CARTOON = 'cartoon',
  CINEMATIC = 'cinematic',
  ARTISTIC = 'artistic',
  ABSTRACT = 'abstract',
  MINIMAL = 'minimal',
}

/**
 * Job Status
 * สถานะของ job
 */
export enum JobStatus {
  /** รอดำเนินการ */
  PENDING = 'pending',
  /** กำลังดำเนินการ */
  PROCESSING = 'processing',
  /** สำเร็จ */
  COMPLETED = 'completed',
  /** ล้มเหลว */
  FAILED = 'failed',
  /** ถูกยกเลิก */
  CANCELLED = 'cancelled',
}

/**
 * Video Generation Configuration
 * การตั้งค่าสำหรับสร้างวีดีโอ
 */
export interface VideoGenerationConfig {
  /** Provider ที่จะใช้ (default: freepik) */
  provider?: VideoProvider;

  /** โหมดการสร้าง */
  mode: VideoGenerationMode;

  /** Prompt/คำอธิบาย */
  prompt: string;

  /** Negative prompt (สิ่งที่ไม่ต้องการ) */
  negativePrompt?: string;

  /** ความยาววีดีโอ (วินาที) */
  duration: number;

  /** อัตราส่วน */
  aspectRatio?: AspectRatio;

  /** สไตล์ */
  style?: VideoStyle;

  /** คุณภาพ */
  quality?: VideoQuality;

  /** FPS */
  fps?: number;

  /** รูปภาพต้นแบบ (สำหรับ image-to-video) */
  sourceImage?: string;

  /** วีดีโอต้นแบบ (สำหรับ video-to-video) */
  sourceVideo?: string;

  /** Seed สำหรับการสร้างแบบสุ่ม (เพื่อ reproducibility) */
  seed?: number;

  /** จำนวนครั้งที่ต้องการสร้าง */
  numberOfOutputs?: number;

  /** ความเข้มของการแปลง (0-1) */
  strength?: number;

  /** การตั้งค่าเพิ่มเติมเฉพาะ provider */
  providerSpecific?: Record<string, any>;
}

/**
 * Video Result
 * ผลลัพธ์จากการสร้างวีดีโอ
 */
export interface VideoResult {
  /** สำเร็จหรือไม่ */
  success: boolean;

  /** Job ID */
  jobId: string;

  /** URL ของวีดีโอ */
  videoUrl?: string;

  /** Local path ของวีดีโอ */
  videoPath?: string;

  /** URL ของ thumbnail */
  thumbnailUrl?: string;

  /** Metadata */
  metadata: VideoMetadata;

  /** Error message (ถ้ามี) */
  error?: string;

  /** เวลาที่ใช้ในการสร้าง (วินาที) */
  processingTime?: number;
}

/**
 * Video Metadata
 * ข้อมูลเพิ่มเติมของวีดีโอ
 */
export interface VideoMetadata {
  /** Provider ที่ใช้ */
  provider: VideoProvider;

  /** ความยาววีดีโอ (วินาที) */
  duration: number;

  /** ความกว้าง (pixels) */
  width: number;

  /** ความสูง (pixels) */
  height: number;

  /** อัตราส่วน */
  aspectRatio: string;

  /** FPS */
  fps: number;

  /** ขนาดไฟล์ (bytes) */
  fileSize: number;

  /** Format */
  format: string;

  /** Codec */
  codec?: string;

  /** Bitrate */
  bitrate?: number;

  /** Prompt ที่ใช้ */
  prompt: string;

  /** Negative prompt */
  negativePrompt?: string;

  /** Seed */
  seed?: number;

  /** เวลาที่สร้าง */
  createdAt: Date;

  /** ข้อมูลเพิ่มเติม */
  extra?: Record<string, any>;
}

/**
 * Video Job
 * ข้อมูลของ job ในการสร้างวีดีโอ
 */
export interface VideoJob {
  /** Job ID */
  id: string;

  /** การตั้งค่า */
  config: VideoGenerationConfig;

  /** สถานะ */
  status: JobStatus;

  /** ความคืบหน้า (0-100) */
  progress: number;

  /** ผลลัพธ์ */
  result?: VideoResult;

  /** Error */
  error?: string;

  /** เวลาที่สร้าง job */
  createdAt: Date;

  /** เวลาที่เริ่มประมวลผล */
  startedAt?: Date;

  /** เวลาที่เสร็จสิ้น */
  completedAt?: Date;

  /** จำนวนครั้งที่ retry */
  retryCount: number;

  /** จำนวน retry สูงสุด */
  maxRetries: number;

  /** Webhook URL สำหรับ notification */
  webhookUrl?: string;
}

/**
 * Freepik Specific Configuration
 * การตั้งค่าเฉพาะสำหรับ Freepik/Pikaso AI
 */
export interface FreepikConfig {
  /** Animation style */
  animationStyle?: 'smooth' | 'dynamic' | 'dramatic';

  /** Camera movement */
  cameraMovement?: 'static' | 'pan' | 'zoom' | 'rotate' | 'orbit';

  /** Motion intensity (1-10) */
  motionIntensity?: number;

  /** Color palette */
  colorPalette?: 'vibrant' | 'pastel' | 'monochrome' | 'warm' | 'cool';

  /** Lighting */
  lighting?: 'natural' | 'studio' | 'dramatic' | 'soft' | 'neon';

  /** End frame (จบที่ไหน) */
  endFrame?: 'zoom_in' | 'zoom_out' | 'fade' | 'still';
}

/**
 * Provider Credentials
 * ข้อมูลการเข้าสู่ระบบของแต่ละ provider
 */
export interface ProviderCredentials {
  /** Provider name */
  provider: VideoProvider;

  /** Email */
  email?: string;

  /** Password */
  password?: string;

  /** API Key */
  apiKey?: string;

  /** Access Token */
  accessToken?: string;

  /** Refresh Token */
  refreshToken?: string;

  /** Session data */
  sessionData?: any;

  /** เวลาที่ token หมดอายุ */
  expiresAt?: Date;
}

/**
 * Download Progress Callback
 * Callback สำหรับติดตามความคืบหน้าการดาวน์โหลด
 */
export type DownloadProgressCallback = (progress: {
  /** Bytes ที่ดาวน์โหลดแล้ว */
  downloaded: number;
  /** ขนาดทั้งหมด */
  total: number;
  /** เปอร์เซ็นต์ (0-100) */
  percent: number;
  /** ความเร็ว (bytes/sec) */
  speed: number;
}) => void;
