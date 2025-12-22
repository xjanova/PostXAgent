<?php

namespace Database\Seeders;

use Illuminate\Database\Seeder;
use App\Models\RentalPackage;

class RentalPackageSeeder extends Seeder
{
    /**
     * Run the database seeds.
     */
    public function run(): void
    {
        // Use the createDefaultPackages method defined in the model
        RentalPackage::createDefaultPackages();
    }
}
