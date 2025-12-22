<?php

declare(strict_types=1);

namespace App\Http\Middleware;

use Closure;
use Illuminate\Http\Request;
use Symfony\Component\HttpFoundation\Response;

class SetLocale
{
    /**
     * Supported locales
     */
    protected array $locales = ['th', 'en'];

    /**
     * Handle an incoming request.
     */
    public function handle(Request $request, Closure $next): Response
    {
        $locale = $this->determineLocale($request);

        app()->setLocale($locale);

        $response = $next($request);

        // Add content language header
        if ($response instanceof Response) {
            $response->headers->set('Content-Language', $locale);
        }

        return $response;
    }

    /**
     * Determine the locale for the request
     */
    protected function determineLocale(Request $request): string
    {
        // 1. Check query parameter (?lang=th)
        if ($request->has('lang') && in_array($request->query('lang'), $this->locales)) {
            return $request->query('lang');
        }

        // 2. Check Accept-Language header
        if ($request->hasHeader('Accept-Language')) {
            $acceptLanguage = $request->header('Accept-Language');
            $preferred = $this->parseAcceptLanguage($acceptLanguage);

            if ($preferred && in_array($preferred, $this->locales)) {
                return $preferred;
            }
        }

        // 3. Check X-Locale header (custom header for API)
        if ($request->hasHeader('X-Locale') && in_array($request->header('X-Locale'), $this->locales)) {
            return $request->header('X-Locale');
        }

        // 4. Check authenticated user's preferred language
        if ($request->user() && $request->user()->language) {
            $userLang = $request->user()->language;
            if (in_array($userLang, $this->locales)) {
                return $userLang;
            }
        }

        // 5. Default to Thai (primary market)
        return config('app.locale', 'th');
    }

    /**
     * Parse Accept-Language header and get preferred language
     */
    protected function parseAcceptLanguage(string $header): ?string
    {
        $languages = [];

        // Parse Accept-Language header (e.g., "en-US,en;q=0.9,th;q=0.8")
        preg_match_all('/([a-zA-Z\-]+)(;q=([0-9.]+))?/', $header, $matches);

        if (!empty($matches[1])) {
            foreach ($matches[1] as $index => $lang) {
                // Get just the language code (e.g., "en" from "en-US")
                $langCode = strtolower(substr($lang, 0, 2));
                $quality = isset($matches[3][$index]) && $matches[3][$index] !== ''
                    ? (float) $matches[3][$index]
                    : 1.0;

                $languages[$langCode] = $quality;
            }

            // Sort by quality descending
            arsort($languages);

            // Return highest quality match
            foreach (array_keys($languages) as $lang) {
                if (in_array($lang, $this->locales)) {
                    return $lang;
                }
            }
        }

        return null;
    }
}
