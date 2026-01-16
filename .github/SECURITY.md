# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 1.x.x   | :white_check_mark: |
| < 1.0   | :x:                |

## Reporting a Vulnerability

We take security seriously. If you discover a security vulnerability in PostXAgent, please report it responsibly.

### How to Report

1. **DO NOT** create a public GitHub issue for security vulnerabilities
2. Email the maintainers directly or use GitHub's private vulnerability reporting feature
3. Include as much detail as possible:
   - Type of vulnerability
   - Steps to reproduce
   - Potential impact
   - Suggested fix (if any)

### What to Expect

- **Acknowledgment**: We will acknowledge receipt within 48 hours
- **Assessment**: We will assess the vulnerability and its impact
- **Resolution**: We aim to release a fix within 7-14 days for critical issues
- **Disclosure**: We will coordinate with you on public disclosure timing

### Scope

The following are in scope for security reports:

- Laravel Backend (PHP)
- AI Manager Core (C#)
- AI Manager API
- AI Manager UI
- Authentication/Authorization issues
- Data exposure vulnerabilities
- API security issues

### Out of Scope

- Social media platform security (report to respective platforms)
- Third-party AI provider security (report to respective providers)
- Issues in dependencies (report to upstream maintainers)

## Security Best Practices

When deploying PostXAgent:

1. **Environment Variables**: Never commit `.env` files or API keys
2. **HTTPS**: Always use HTTPS in production
3. **Updates**: Keep all dependencies up to date
4. **Access Control**: Implement proper access controls for the admin panel
5. **API Keys**: Rotate API keys regularly
6. **Monitoring**: Monitor for unusual activity

## Acknowledgments

We appreciate the security research community's efforts in helping keep PostXAgent secure. Contributors who report valid security issues will be acknowledged (if they wish) in our release notes.
