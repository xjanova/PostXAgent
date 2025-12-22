<?php

namespace Database\Seeders;

use App\Models\WorkflowTemplate;
use Illuminate\Database\Seeder;

class WorkflowTemplateSeeder extends Seeder
{
    public function run(): void
    {
        $templates = [
            // ═══════════════════════════════════════════════════════════════
            // MARKETING TEMPLATES
            // ═══════════════════════════════════════════════════════════════
            [
                'name' => 'Product Promotion Post',
                'name_th' => 'โพสต์โปรโมทสินค้า',
                'description' => 'Create engaging promotional posts for products',
                'description_th' => 'สร้างโพสต์โปรโมทสินค้าที่น่าสนใจ',
                'category' => 'marketing',
                'icon' => 'ShoppingCart',
                'supported_platforms' => ['facebook', 'instagram', 'twitter', 'line', 'tiktok'],
                'variables' => [
                    'product_name' => ['type' => 'text', 'label' => 'Product Name', 'label_th' => 'ชื่อสินค้า', 'required' => true],
                    'product_description' => ['type' => 'textarea', 'label' => 'Description', 'label_th' => 'รายละเอียด', 'required' => true],
                    'price' => ['type' => 'text', 'label' => 'Price', 'label_th' => 'ราคา', 'required' => true],
                    'discount' => ['type' => 'text', 'label' => 'Discount (optional)', 'label_th' => 'ส่วนลด (ถ้ามี)', 'required' => false],
                ],
                'workflow_json' => $this->createProductPromoWorkflow(),
                'is_system' => true,
                'is_active' => true,
            ],
            [
                'name' => 'Flash Sale Announcement',
                'name_th' => 'ประกาศ Flash Sale',
                'description' => 'Create urgent flash sale announcements',
                'description_th' => 'สร้างประกาศ Flash Sale ที่กระตุ้นความเร่งด่วน',
                'category' => 'marketing',
                'icon' => 'FlashOn',
                'supported_platforms' => ['facebook', 'instagram', 'line', 'tiktok'],
                'variables' => [
                    'sale_name' => ['type' => 'text', 'label' => 'Sale Name', 'label_th' => 'ชื่อโปรโมชั่น', 'required' => true],
                    'discount' => ['type' => 'text', 'label' => 'Discount', 'label_th' => 'ส่วนลด', 'required' => true],
                    'duration' => ['type' => 'text', 'label' => 'Duration', 'label_th' => 'ระยะเวลา', 'required' => true],
                ],
                'workflow_json' => $this->createFlashSaleWorkflow(),
                'is_system' => true,
                'is_active' => true,
            ],

            // ═══════════════════════════════════════════════════════════════
            // CONTENT TEMPLATES
            // ═══════════════════════════════════════════════════════════════
            [
                'name' => 'Educational Content',
                'name_th' => 'เนื้อหาให้ความรู้',
                'description' => 'Create educational and informative content',
                'description_th' => 'สร้างเนื้อหาให้ความรู้และข้อมูลที่เป็นประโยชน์',
                'category' => 'content',
                'icon' => 'School',
                'supported_platforms' => ['facebook', 'instagram', 'twitter', 'linkedin', 'threads'],
                'variables' => [
                    'topic' => ['type' => 'text', 'label' => 'Topic', 'label_th' => 'หัวข้อ', 'required' => true],
                    'key_points' => ['type' => 'textarea', 'label' => 'Key Points', 'label_th' => 'ประเด็นสำคัญ', 'required' => true],
                    'target_audience' => ['type' => 'text', 'label' => 'Target Audience', 'label_th' => 'กลุ่มเป้าหมาย', 'required' => false],
                    'format' => ['type' => 'select', 'label' => 'Format', 'label_th' => 'รูปแบบ', 'options' => ['tips', 'how-to', 'facts', 'guide']],
                ],
                'workflow_json' => $this->createEducationalWorkflow(),
                'is_system' => true,
                'is_active' => true,
            ],
            [
                'name' => 'Entertainment Post',
                'name_th' => 'โพสต์บันเทิง',
                'description' => 'Create fun and entertaining content',
                'description_th' => 'สร้างเนื้อหาสนุกสนานและบันเทิง',
                'category' => 'content',
                'icon' => 'EmojiEmotions',
                'supported_platforms' => ['facebook', 'instagram', 'tiktok', 'twitter'],
                'variables' => [
                    'theme' => ['type' => 'text', 'label' => 'Theme', 'label_th' => 'ธีม', 'required' => true],
                    'tone' => ['type' => 'select', 'label' => 'Tone', 'label_th' => 'โทนเสียง', 'options' => ['funny', 'relatable', 'inspirational', 'nostalgic']],
                ],
                'workflow_json' => $this->createEntertainmentWorkflow(),
                'is_system' => true,
                'is_active' => true,
            ],

            // ═══════════════════════════════════════════════════════════════
            // ENGAGEMENT TEMPLATES
            // ═══════════════════════════════════════════════════════════════
            [
                'name' => 'Engagement Question',
                'name_th' => 'คำถามสร้างการมีส่วนร่วม',
                'description' => 'Ask engaging questions to boost interaction',
                'description_th' => 'ถามคำถามที่กระตุ้นการมีส่วนร่วม',
                'category' => 'engagement',
                'icon' => 'QuestionAnswer',
                'supported_platforms' => ['facebook', 'instagram', 'twitter', 'threads'],
                'variables' => [
                    'topic' => ['type' => 'text', 'label' => 'Topic', 'label_th' => 'หัวข้อ', 'required' => true],
                    'question_style' => ['type' => 'select', 'label' => 'Style', 'label_th' => 'รูปแบบ', 'options' => ['opinion', 'experience', 'preference', 'this_or_that']],
                ],
                'workflow_json' => $this->createEngagementQuestionWorkflow(),
                'is_system' => true,
                'is_active' => true,
            ],
            [
                'name' => 'Poll/Survey',
                'name_th' => 'โพล/แบบสำรวจ',
                'description' => 'Create polls and surveys for audience feedback',
                'description_th' => 'สร้างโพลและแบบสำรวจเพื่อรับ feedback',
                'category' => 'engagement',
                'icon' => 'Poll',
                'supported_platforms' => ['facebook', 'instagram', 'twitter'],
                'variables' => [
                    'question' => ['type' => 'text', 'label' => 'Poll Question', 'label_th' => 'คำถาม', 'required' => true],
                    'options' => ['type' => 'textarea', 'label' => 'Options (one per line)', 'label_th' => 'ตัวเลือก (บรรทัดละ 1)', 'required' => true],
                ],
                'workflow_json' => $this->createPollWorkflow(),
                'is_system' => true,
                'is_active' => true,
            ],

            // ═══════════════════════════════════════════════════════════════
            // SEEK AND POST TEMPLATES
            // ═══════════════════════════════════════════════════════════════
            [
                'name' => 'Group Discovery & Join',
                'name_th' => 'ค้นหาและขอเข้ากลุ่ม',
                'description' => 'Automatically discover and request to join relevant groups',
                'description_th' => 'ค้นหากลุ่มที่เกี่ยวข้องและขอเข้าร่วมโดยอัตโนมัติ',
                'category' => 'seek_and_post',
                'icon' => 'TravelExplore',
                'supported_platforms' => ['facebook', 'line', 'telegram'],
                'variables' => [
                    'keywords' => ['type' => 'multiselect', 'label' => 'Keywords', 'label_th' => 'คีย์เวิร์ด', 'required' => true],
                    'min_members' => ['type' => 'number', 'label' => 'Min Members', 'label_th' => 'สมาชิกขั้นต่ำ', 'default' => 100],
                    'max_joins_per_day' => ['type' => 'number', 'label' => 'Max Joins/Day', 'label_th' => 'ขอเข้าสูงสุด/วัน', 'default' => 10],
                ],
                'workflow_json' => $this->createGroupDiscoveryWorkflow(),
                'is_system' => true,
                'is_active' => true,
            ],
            [
                'name' => 'Smart Group Post',
                'name_th' => 'โพสต์กลุ่มอัจฉริยะ',
                'description' => 'Intelligently post to joined groups with optimal timing',
                'description_th' => 'โพสต์ไปยังกลุ่มที่เข้าร่วมอยู่อย่างชาญฉลาด',
                'category' => 'seek_and_post',
                'icon' => 'AutoMode',
                'supported_platforms' => ['facebook', 'line', 'telegram'],
                'variables' => [
                    'target_groups' => ['type' => 'multiselect', 'label' => 'Target Group Keywords', 'label_th' => 'คีย์เวิร์ดกลุ่มเป้าหมาย', 'required' => true],
                    'content_topic' => ['type' => 'text', 'label' => 'Content Topic', 'label_th' => 'หัวข้อเนื้อหา', 'required' => true],
                    'posts_per_group' => ['type' => 'number', 'label' => 'Posts per Group', 'label_th' => 'โพสต์ต่อกลุ่ม', 'default' => 1],
                    'smart_timing' => ['type' => 'boolean', 'label' => 'Smart Timing', 'label_th' => 'จับเวลาอัตโนมัติ', 'default' => true],
                ],
                'workflow_json' => $this->createSmartGroupPostWorkflow(),
                'is_system' => true,
                'is_active' => true,
            ],

            // ═══════════════════════════════════════════════════════════════
            // PLATFORM SPECIFIC TEMPLATES
            // ═══════════════════════════════════════════════════════════════
            [
                'name' => 'Instagram Story',
                'name_th' => 'Instagram Story',
                'description' => 'Create engaging Instagram Stories',
                'description_th' => 'สร้าง Instagram Stories ที่น่าสนใจ',
                'category' => 'platform_specific',
                'icon' => 'Instagram',
                'supported_platforms' => ['instagram'],
                'variables' => [
                    'story_type' => ['type' => 'select', 'label' => 'Story Type', 'label_th' => 'ประเภท Story', 'options' => ['promotion', 'behind_scenes', 'poll', 'countdown', 'question'], 'required' => true],
                    'content' => ['type' => 'textarea', 'label' => 'Content', 'label_th' => 'เนื้อหา', 'required' => true],
                ],
                'workflow_json' => $this->createInstagramStoryWorkflow(),
                'is_system' => true,
                'is_active' => true,
            ],
            [
                'name' => 'TikTok Script',
                'name_th' => 'สคริปต์ TikTok',
                'description' => 'Generate TikTok video scripts with hooks',
                'description_th' => 'สร้างสคริปต์วิดีโอ TikTok พร้อม hook',
                'category' => 'platform_specific',
                'icon' => 'MusicNote',
                'supported_platforms' => ['tiktok'],
                'variables' => [
                    'topic' => ['type' => 'text', 'label' => 'Video Topic', 'label_th' => 'หัวข้อวิดีโอ', 'required' => true],
                    'duration' => ['type' => 'select', 'label' => 'Duration', 'label_th' => 'ความยาว', 'options' => ['15s', '30s', '60s', '3min']],
                    'style' => ['type' => 'select', 'label' => 'Style', 'label_th' => 'สไตล์', 'options' => ['educational', 'trending', 'storytelling', 'comedy']],
                ],
                'workflow_json' => $this->createTikTokScriptWorkflow(),
                'is_system' => true,
                'is_active' => true,
            ],
            [
                'name' => 'LINE Broadcast',
                'name_th' => 'LINE Broadcast',
                'description' => 'Create LINE broadcast messages',
                'description_th' => 'สร้างข้อความ LINE Broadcast',
                'category' => 'platform_specific',
                'icon' => 'Chat',
                'supported_platforms' => ['line'],
                'variables' => [
                    'message_type' => ['type' => 'select', 'label' => 'Message Type', 'label_th' => 'ประเภทข้อความ', 'options' => ['text', 'rich', 'flex', 'image']],
                    'content' => ['type' => 'textarea', 'label' => 'Message Content', 'label_th' => 'เนื้อหาข้อความ', 'required' => true],
                ],
                'workflow_json' => $this->createLineBroadcastWorkflow(),
                'is_system' => true,
                'is_active' => true,
            ],
            [
                'name' => 'Twitter Thread',
                'name_th' => 'Twitter Thread',
                'description' => 'Create engaging Twitter/X threads',
                'description_th' => 'สร้าง Thread บน Twitter/X ที่น่าสนใจ',
                'category' => 'platform_specific',
                'icon' => 'Twitter',
                'supported_platforms' => ['twitter'],
                'variables' => [
                    'topic' => ['type' => 'text', 'label' => 'Thread Topic', 'label_th' => 'หัวข้อ Thread', 'required' => true],
                    'key_points' => ['type' => 'textarea', 'label' => 'Key Points', 'label_th' => 'ประเด็นสำคัญ', 'required' => true],
                    'thread_length' => ['type' => 'number', 'label' => 'Number of Tweets', 'label_th' => 'จำนวน Tweet', 'default' => 5],
                ],
                'workflow_json' => $this->createTwitterThreadWorkflow(),
                'is_system' => true,
                'is_active' => true,
            ],

            // ═══════════════════════════════════════════════════════════════
            // SPECIAL TEMPLATES
            // ═══════════════════════════════════════════════════════════════
            [
                'name' => 'Holiday Greeting',
                'name_th' => 'คำอวยพรวันหยุด',
                'description' => 'Create holiday and special occasion greetings',
                'description_th' => 'สร้างคำอวยพรสำหรับวันหยุดและโอกาสพิเศษ',
                'category' => 'special',
                'icon' => 'Celebration',
                'supported_platforms' => ['facebook', 'instagram', 'twitter', 'line', 'threads'],
                'variables' => [
                    'occasion' => ['type' => 'text', 'label' => 'Occasion', 'label_th' => 'โอกาส', 'required' => true],
                    'brand_name' => ['type' => 'text', 'label' => 'Brand Name', 'label_th' => 'ชื่อแบรนด์', 'required' => false],
                    'tone' => ['type' => 'select', 'label' => 'Tone', 'label_th' => 'โทนเสียง', 'options' => ['formal', 'casual', 'warm', 'festive']],
                ],
                'workflow_json' => $this->createHolidayGreetingWorkflow(),
                'is_system' => true,
                'is_active' => true,
            ],
            [
                'name' => 'Thank You Post',
                'name_th' => 'โพสต์ขอบคุณ',
                'description' => 'Create thank you messages for customers/followers',
                'description_th' => 'สร้างข้อความขอบคุณลูกค้า/ผู้ติดตาม',
                'category' => 'special',
                'icon' => 'Favorite',
                'supported_platforms' => ['facebook', 'instagram', 'twitter', 'line'],
                'variables' => [
                    'milestone' => ['type' => 'text', 'label' => 'Milestone', 'label_th' => 'เหตุการณ์สำคัญ', 'required' => true],
                    'special_offer' => ['type' => 'text', 'label' => 'Special Offer (optional)', 'label_th' => 'ข้อเสนอพิเศษ (ถ้ามี)', 'required' => false],
                ],
                'workflow_json' => $this->createThankYouWorkflow(),
                'is_system' => true,
                'is_active' => true,
            ],
        ];

        foreach ($templates as $template) {
            WorkflowTemplate::updateOrCreate(
                ['name' => $template['name'], 'is_system' => true],
                $template
            );
        }

        $this->command->info('Created ' . count($templates) . ' workflow templates.');
    }

    // ═══════════════════════════════════════════════════════════════════════
    // WORKFLOW JSON GENERATORS
    // ═══════════════════════════════════════════════════════════════════════

    private function createProductPromoWorkflow(): string
    {
        return json_encode([
            'name' => 'Product Promotion',
            'nodes' => [
                ['id' => 'input_1', 'type' => 'TextInput', 'x' => 100, 'y' => 100, 'data' => ['label' => 'Product Name', 'variableKey' => 'product_name']],
                ['id' => 'input_2', 'type' => 'TextInput', 'x' => 100, 'y' => 200, 'data' => ['label' => 'Description', 'variableKey' => 'product_description']],
                ['id' => 'input_3', 'type' => 'TextInput', 'x' => 100, 'y' => 300, 'data' => ['label' => 'Price', 'variableKey' => 'price']],
                ['id' => 'ai_1', 'type' => 'AITextGenerator', 'x' => 400, 'y' => 150, 'data' => [
                    'prompt' => 'สร้างข้อความโปรโมทสินค้าภาษาไทยที่น่าสนใจสำหรับ {product_name}\nรายละเอียด: {product_description}\nราคา: {price}\n\nเขียนให้กระชับ น่าสนใจ เหมาะสำหรับโพสต์ใน Social Media',
                    'maxTokens' => 300,
                ]],
                ['id' => 'hashtag_1', 'type' => 'AITextGenerator', 'x' => 400, 'y' => 300, 'data' => [
                    'prompt' => 'สร้าง 5-10 hashtags ภาษาไทยและอังกฤษที่เกี่ยวข้องกับ {product_name}',
                    'maxTokens' => 100,
                ]],
                ['id' => 'combine_1', 'type' => 'TextCombiner', 'x' => 700, 'y' => 200, 'data' => ['separator' => '\n\n']],
                ['id' => 'output_1', 'type' => 'Output', 'x' => 900, 'y' => 200, 'data' => ['label' => 'Final Post']],
            ],
            'connections' => [
                ['from' => 'input_1', 'to' => 'ai_1', 'fromPort' => 'output', 'toPort' => 'input'],
                ['from' => 'input_2', 'to' => 'ai_1', 'fromPort' => 'output', 'toPort' => 'context'],
                ['from' => 'ai_1', 'to' => 'combine_1', 'fromPort' => 'output', 'toPort' => 'text1'],
                ['from' => 'hashtag_1', 'to' => 'combine_1', 'fromPort' => 'output', 'toPort' => 'text2'],
                ['from' => 'combine_1', 'to' => 'output_1', 'fromPort' => 'output', 'toPort' => 'input'],
            ],
        ]);
    }

    private function createFlashSaleWorkflow(): string
    {
        return json_encode([
            'name' => 'Flash Sale',
            'nodes' => [
                ['id' => 'ai_1', 'type' => 'AITextGenerator', 'x' => 300, 'y' => 150, 'data' => [
                    'prompt' => 'สร้างข้อความ Flash Sale ที่สร้างความเร่งด่วน:\nชื่อโปรโมชั่น: {sale_name}\nส่วนลด: {discount}\nระยะเวลา: {duration}\n\nใช้ emoji และภาษาที่กระตุ้นให้รีบซื้อ',
                    'maxTokens' => 250,
                ]],
                ['id' => 'output_1', 'type' => 'Output', 'x' => 600, 'y' => 150, 'data' => ['label' => 'Flash Sale Post']],
            ],
            'connections' => [
                ['from' => 'ai_1', 'to' => 'output_1', 'fromPort' => 'output', 'toPort' => 'input'],
            ],
        ]);
    }

    private function createEducationalWorkflow(): string
    {
        return json_encode([
            'name' => 'Educational Content',
            'nodes' => [
                ['id' => 'ai_1', 'type' => 'AITextGenerator', 'x' => 300, 'y' => 150, 'data' => [
                    'prompt' => 'สร้างเนื้อหาให้ความรู้เกี่ยวกับ: {topic}\nประเด็นสำคัญ: {key_points}\nกลุ่มเป้าหมาย: {target_audience}\nรูปแบบ: {format}\n\nเขียนให้อ่านง่าย เข้าใจง่าย เหมาะกับ Social Media',
                    'maxTokens' => 400,
                ]],
                ['id' => 'output_1', 'type' => 'Output', 'x' => 600, 'y' => 150, 'data' => ['label' => 'Educational Post']],
            ],
            'connections' => [
                ['from' => 'ai_1', 'to' => 'output_1', 'fromPort' => 'output', 'toPort' => 'input'],
            ],
        ]);
    }

    private function createEntertainmentWorkflow(): string
    {
        return json_encode([
            'name' => 'Entertainment Post',
            'nodes' => [
                ['id' => 'ai_1', 'type' => 'AITextGenerator', 'x' => 300, 'y' => 150, 'data' => [
                    'prompt' => 'สร้างโพสต์บันเทิงที่: {tone}\nธีม: {theme}\n\nเขียนให้สนุก น่าสนใจ มี engagement สูง',
                    'maxTokens' => 250,
                ]],
                ['id' => 'output_1', 'type' => 'Output', 'x' => 600, 'y' => 150, 'data' => ['label' => 'Entertainment Post']],
            ],
            'connections' => [
                ['from' => 'ai_1', 'to' => 'output_1', 'fromPort' => 'output', 'toPort' => 'input'],
            ],
        ]);
    }

    private function createEngagementQuestionWorkflow(): string
    {
        return json_encode([
            'name' => 'Engagement Question',
            'nodes' => [
                ['id' => 'ai_1', 'type' => 'AITextGenerator', 'x' => 300, 'y' => 150, 'data' => [
                    'prompt' => 'สร้างคำถามเพื่อกระตุ้นการมีส่วนร่วม:\nหัวข้อ: {topic}\nสไตล์คำถาม: {question_style}\n\nต้องเป็นคำถามที่คนอยากตอบ อยากแชร์ความคิดเห็น',
                    'maxTokens' => 200,
                ]],
                ['id' => 'output_1', 'type' => 'Output', 'x' => 600, 'y' => 150, 'data' => ['label' => 'Question Post']],
            ],
            'connections' => [
                ['from' => 'ai_1', 'to' => 'output_1', 'fromPort' => 'output', 'toPort' => 'input'],
            ],
        ]);
    }

    private function createPollWorkflow(): string
    {
        return json_encode([
            'name' => 'Poll/Survey',
            'nodes' => [
                ['id' => 'ai_1', 'type' => 'AITextGenerator', 'x' => 300, 'y' => 150, 'data' => [
                    'prompt' => 'สร้างโพลจากข้อมูล:\nคำถาม: {question}\nตัวเลือก: {options}\n\nเขียนให้น่าสนใจ กระตุ้นให้คนอยากร่วมโหวต',
                    'maxTokens' => 200,
                ]],
                ['id' => 'output_1', 'type' => 'Output', 'x' => 600, 'y' => 150, 'data' => ['label' => 'Poll Post']],
            ],
            'connections' => [
                ['from' => 'ai_1', 'to' => 'output_1', 'fromPort' => 'output', 'toPort' => 'input'],
            ],
        ]);
    }

    private function createGroupDiscoveryWorkflow(): string
    {
        return json_encode([
            'name' => 'Group Discovery',
            'nodes' => [
                ['id' => 'search_1', 'type' => 'GroupSearch', 'x' => 300, 'y' => 150, 'data' => [
                    'keywords' => '{keywords}',
                    'minMembers' => '{min_members}',
                    'maxJoinsPerDay' => '{max_joins_per_day}',
                ]],
                ['id' => 'filter_1', 'type' => 'GroupFilter', 'x' => 500, 'y' => 150, 'data' => [
                    'excludeJoined' => true,
                    'excludeBanned' => true,
                ]],
                ['id' => 'join_1', 'type' => 'GroupJoinRequest', 'x' => 700, 'y' => 150, 'data' => []],
            ],
            'connections' => [
                ['from' => 'search_1', 'to' => 'filter_1', 'fromPort' => 'groups', 'toPort' => 'groups'],
                ['from' => 'filter_1', 'to' => 'join_1', 'fromPort' => 'filtered', 'toPort' => 'groups'],
            ],
        ]);
    }

    private function createSmartGroupPostWorkflow(): string
    {
        return json_encode([
            'name' => 'Smart Group Post',
            'nodes' => [
                ['id' => 'groups_1', 'type' => 'GetRecommendedGroups', 'x' => 100, 'y' => 150, 'data' => [
                    'keywords' => '{target_groups}',
                ]],
                ['id' => 'ai_1', 'type' => 'AITextGenerator', 'x' => 300, 'y' => 150, 'data' => [
                    'prompt' => 'สร้างโพสต์สำหรับกลุ่มเกี่ยวกับ: {content_topic}\n\nเขียนให้เป็นธรรมชาติ ไม่โฆษณาจนเกินไป เหมาะกับการโพสต์ในกลุ่ม',
                    'maxTokens' => 300,
                ]],
                ['id' => 'post_1', 'type' => 'PostToGroups', 'x' => 500, 'y' => 150, 'data' => [
                    'postsPerGroup' => '{posts_per_group}',
                    'smartTiming' => '{smart_timing}',
                ]],
            ],
            'connections' => [
                ['from' => 'groups_1', 'to' => 'post_1', 'fromPort' => 'groups', 'toPort' => 'groups'],
                ['from' => 'ai_1', 'to' => 'post_1', 'fromPort' => 'output', 'toPort' => 'content'],
            ],
        ]);
    }

    private function createInstagramStoryWorkflow(): string
    {
        return json_encode([
            'name' => 'Instagram Story',
            'nodes' => [
                ['id' => 'ai_1', 'type' => 'AITextGenerator', 'x' => 300, 'y' => 150, 'data' => [
                    'prompt' => 'สร้างเนื้อหาสำหรับ Instagram Story:\nประเภท: {story_type}\nเนื้อหาหลัก: {content}\n\nเขียนให้สั้น กระชับ น่าสนใจ เหมาะกับ Story format',
                    'maxTokens' => 150,
                ]],
                ['id' => 'post_1', 'type' => 'PostToInstagram', 'x' => 550, 'y' => 150, 'data' => [
                    'postType' => 'story',
                ]],
            ],
            'connections' => [
                ['from' => 'ai_1', 'to' => 'post_1', 'fromPort' => 'output', 'toPort' => 'content'],
            ],
        ]);
    }

    private function createTikTokScriptWorkflow(): string
    {
        return json_encode([
            'name' => 'TikTok Script',
            'nodes' => [
                ['id' => 'ai_1', 'type' => 'AITextGenerator', 'x' => 300, 'y' => 150, 'data' => [
                    'prompt' => 'สร้างสคริปต์วิดีโอ TikTok:\nหัวข้อ: {topic}\nความยาว: {duration}\nสไตล์: {style}\n\nโครงสร้าง:\n1. Hook (3 วินาทีแรก) - ต้องดึงดูดทันที\n2. เนื้อหาหลัก\n3. Call to Action',
                    'maxTokens' => 400,
                ]],
                ['id' => 'output_1', 'type' => 'Output', 'x' => 600, 'y' => 150, 'data' => ['label' => 'TikTok Script']],
            ],
            'connections' => [
                ['from' => 'ai_1', 'to' => 'output_1', 'fromPort' => 'output', 'toPort' => 'input'],
            ],
        ]);
    }

    private function createLineBroadcastWorkflow(): string
    {
        return json_encode([
            'name' => 'LINE Broadcast',
            'nodes' => [
                ['id' => 'ai_1', 'type' => 'AITextGenerator', 'x' => 300, 'y' => 150, 'data' => [
                    'prompt' => 'สร้างข้อความ LINE Broadcast:\nประเภท: {message_type}\nเนื้อหา: {content}\n\nเขียนให้เป็นกันเอง อ่านง่าย เหมาะกับการส่งใน LINE',
                    'maxTokens' => 250,
                ]],
                ['id' => 'post_1', 'type' => 'PostToLINE', 'x' => 550, 'y' => 150, 'data' => [
                    'messageType' => '{message_type}',
                ]],
            ],
            'connections' => [
                ['from' => 'ai_1', 'to' => 'post_1', 'fromPort' => 'output', 'toPort' => 'content'],
            ],
        ]);
    }

    private function createTwitterThreadWorkflow(): string
    {
        return json_encode([
            'name' => 'Twitter Thread',
            'nodes' => [
                ['id' => 'ai_1', 'type' => 'AITextGenerator', 'x' => 300, 'y' => 150, 'data' => [
                    'prompt' => 'สร้าง Twitter Thread จำนวน {thread_length} ทวีต:\nหัวข้อ: {topic}\nประเด็นสำคัญ: {key_points}\n\nแต่ละทวีตต้องไม่เกิน 280 ตัวอักษร\nทวีตแรกต้องดึงดูดและมี emoji\nใส่หมายเลขลำดับ เช่น 1/ 2/ 3/',
                    'maxTokens' => 500,
                ]],
                ['id' => 'output_1', 'type' => 'Output', 'x' => 600, 'y' => 150, 'data' => ['label' => 'Twitter Thread']],
            ],
            'connections' => [
                ['from' => 'ai_1', 'to' => 'output_1', 'fromPort' => 'output', 'toPort' => 'input'],
            ],
        ]);
    }

    private function createHolidayGreetingWorkflow(): string
    {
        return json_encode([
            'name' => 'Holiday Greeting',
            'nodes' => [
                ['id' => 'ai_1', 'type' => 'AITextGenerator', 'x' => 300, 'y' => 150, 'data' => [
                    'prompt' => 'สร้างคำอวยพรสำหรับ:\nโอกาส: {occasion}\nแบรนด์: {brand_name}\nโทนเสียง: {tone}\n\nใส่ emoji ที่เหมาะสม เขียนให้อบอุ่น จริงใจ',
                    'maxTokens' => 200,
                ]],
                ['id' => 'output_1', 'type' => 'Output', 'x' => 600, 'y' => 150, 'data' => ['label' => 'Holiday Greeting']],
            ],
            'connections' => [
                ['from' => 'ai_1', 'to' => 'output_1', 'fromPort' => 'output', 'toPort' => 'input'],
            ],
        ]);
    }

    private function createThankYouWorkflow(): string
    {
        return json_encode([
            'name' => 'Thank You Post',
            'nodes' => [
                ['id' => 'ai_1', 'type' => 'AITextGenerator', 'x' => 300, 'y' => 150, 'data' => [
                    'prompt' => 'สร้างโพสต์ขอบคุณ:\nเหตุการณ์สำคัญ: {milestone}\nข้อเสนอพิเศษ: {special_offer}\n\nเขียนให้จริงใจ อบอุ่น แสดงความขอบคุณจากใจจริง',
                    'maxTokens' => 250,
                ]],
                ['id' => 'output_1', 'type' => 'Output', 'x' => 600, 'y' => 150, 'data' => ['label' => 'Thank You Post']],
            ],
            'connections' => [
                ['from' => 'ai_1', 'to' => 'output_1', 'fromPort' => 'output', 'toPort' => 'input'],
            ],
        ]);
    }
}
