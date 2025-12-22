<?php

namespace Database\Seeders;

use Illuminate\Database\Seeder;
use App\Models\Brand;
use App\Models\User;

class BrandSeeder extends Seeder
{
    /**
     * Run the database seeds.
     */
    public function run(): void
    {
        $demoUser = User::where('email', 'demo@postxagent.com')->first();
        if (!$demoUser) {
            return;
        }

        $brands = [
            [
                'name' => 'TechShop Thailand',
                'description' => 'ร้านค้าอุปกรณ์ไอทีและเทคโนโลยีชั้นนำในประเทศไทย',
                'industry' => 'Technology',
                'target_audience' => 'คนรุ่นใหม่ที่ชอบเทคโนโลยี อายุ 18-35 ปี ชอบติดตามข่าวสารไอที',
                'tone' => 'professional',
                'brand_colors' => ['#2563eb', '#1d4ed8', '#ffffff'],
                'keywords' => ['เทคโนโลยี', 'ไอที', 'สมาร์ทโฟน', 'แกดเจ็ต', 'อุปกรณ์อิเล็กทรอนิกส์'],
                'hashtags' => ['TechShopTH', 'เทคโนโลยี', 'ไอทีไทยแลนด์', 'สมาร์ทโฟน'],
                'website_url' => 'https://techshop.example.com',
                'settings' => [
                    'default_language' => 'th',
                    'auto_hashtag' => true,
                    'emoji_style' => 'moderate',
                ],
                'is_active' => true,
            ],
            [
                'name' => 'Fresh Cafe Bangkok',
                'description' => 'คาเฟ่สุดชิคใจกลางกรุงเทพ เครื่องดื่มอร่อย บรรยากาศดี',
                'industry' => 'Food & Beverage',
                'target_audience' => 'คนทำงานออฟฟิศ นักศึกษา คนรักกาแฟ อายุ 22-40 ปี',
                'tone' => 'friendly',
                'brand_colors' => ['#059669', '#047857', '#fef3c7'],
                'keywords' => ['กาแฟ', 'คาเฟ่', 'เครื่องดื่ม', 'ขนมหวาน', 'คาเฟ่กรุงเทพ'],
                'hashtags' => ['FreshCafeBKK', 'คาเฟ่กรุงเทพ', 'กาแฟดี', 'CoffeeLover'],
                'website_url' => 'https://freshcafe.example.com',
                'settings' => [
                    'default_language' => 'th',
                    'auto_hashtag' => true,
                    'emoji_style' => 'heavy',
                ],
                'is_active' => true,
            ],
            [
                'name' => 'Fashion Forward',
                'description' => 'แฟชั่นสุดล้ำ สไตล์ที่ใช่ ราคาที่ชอบ',
                'industry' => 'Fashion',
                'target_audience' => 'ผู้หญิงรักแฟชั่น อายุ 18-30 ปี ชอบตามเทรนด์ใหม่ๆ',
                'tone' => 'trendy',
                'brand_colors' => ['#ec4899', '#db2777', '#fdf2f8'],
                'keywords' => ['แฟชั่น', 'เสื้อผ้า', 'สไตล์', 'เทรนด์', 'OOTD'],
                'hashtags' => ['FashionForwardTH', 'แฟชั่น', 'OOTD', 'StyleInspo'],
                'website_url' => 'https://fashionforward.example.com',
                'settings' => [
                    'default_language' => 'th',
                    'auto_hashtag' => true,
                    'emoji_style' => 'heavy',
                ],
                'is_active' => true,
            ],
        ];

        foreach ($brands as $brandData) {
            Brand::firstOrCreate(
                [
                    'user_id' => $demoUser->id,
                    'name' => $brandData['name'],
                ],
                $brandData
            );
        }
    }
}
