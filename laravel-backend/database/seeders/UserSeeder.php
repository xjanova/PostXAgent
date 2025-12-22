<?php

namespace Database\Seeders;

use Illuminate\Database\Seeder;
use App\Models\User;
use App\Models\RentalPackage;
use App\Models\UserRental;
use Illuminate\Support\Facades\Hash;

class UserSeeder extends Seeder
{
    /**
     * Run the database seeds.
     */
    public function run(): void
    {
        // Create admin user
        $admin = User::firstOrCreate(
            ['email' => 'admin@postxagent.com'],
            [
                'name' => 'Admin',
                'password' => Hash::make('password'),
                'phone' => '0891234567',
                'company_name' => 'PostXAgent',
                'timezone' => 'Asia/Bangkok',
                'language' => 'th',
                'is_active' => true,
                'email_verified_at' => now(),
            ]
        );
        $admin->assignRole('admin');

        // Create demo user with active rental
        $demoUser = User::firstOrCreate(
            ['email' => 'demo@postxagent.com'],
            [
                'name' => 'Demo User',
                'password' => Hash::make('demo1234'),
                'phone' => '0899876543',
                'company_name' => 'Demo Company',
                'timezone' => 'Asia/Bangkok',
                'language' => 'th',
                'is_active' => true,
                'email_verified_at' => now(),
            ]
        );
        $demoUser->assignRole('user');

        // Give demo user a monthly rental
        $monthlyPackage = RentalPackage::where('name', 'Monthly')->first();
        if ($monthlyPackage) {
            UserRental::firstOrCreate(
                ['user_id' => $demoUser->id, 'status' => UserRental::STATUS_ACTIVE],
                [
                    'rental_package_id' => $monthlyPackage->id,
                    'starts_at' => now(),
                    'expires_at' => now()->addMonth(),
                    'price_paid' => $monthlyPackage->price,
                    'currency' => 'THB',
                    'usage_limits' => $monthlyPackage->getUsageLimits(),
                    'usage_counts' => [
                        'posts' => 0,
                        'ai_generations' => 0,
                    ],
                ]
            );
        }

        // Create test users
        $testUsers = [
            [
                'name' => 'ทดสอบ ผู้ใช้งาน',
                'email' => 'test1@example.com',
                'phone' => '0812345678',
                'company_name' => 'บริษัท ทดสอบ จำกัด',
            ],
            [
                'name' => 'Premium User',
                'email' => 'premium@example.com',
                'phone' => '0823456789',
                'company_name' => 'Premium Corp',
            ],
        ];

        foreach ($testUsers as $userData) {
            $user = User::firstOrCreate(
                ['email' => $userData['email']],
                [
                    ...$userData,
                    'password' => Hash::make('password'),
                    'timezone' => 'Asia/Bangkok',
                    'language' => 'th',
                    'is_active' => true,
                    'email_verified_at' => now(),
                ]
            );
            $user->assignRole('user');
        }

        // Give premium user a yearly rental
        $premiumUser = User::where('email', 'premium@example.com')->first();
        $yearlyPackage = RentalPackage::where('name', 'Yearly')->first();
        if ($premiumUser && $yearlyPackage) {
            UserRental::firstOrCreate(
                ['user_id' => $premiumUser->id, 'status' => UserRental::STATUS_ACTIVE],
                [
                    'rental_package_id' => $yearlyPackage->id,
                    'starts_at' => now(),
                    'expires_at' => now()->addYear(),
                    'price_paid' => $yearlyPackage->price,
                    'currency' => 'THB',
                    'usage_limits' => $yearlyPackage->getUsageLimits(),
                    'usage_counts' => [
                        'posts' => 0,
                        'ai_generations' => 0,
                    ],
                ]
            );
            $premiumUser->assignRole('premium');
        }
    }
}
