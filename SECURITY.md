
# Security Policy

## Reporting Security Vulnerabilities

This project takes security seriously. If you discover a security vulnerability, **please do NOT open a public issue**. Instead, follow this responsible disclosure process.

### How to Report

**Email:** Please report security vulnerabilities privately to:
- Create a [GitHub Security Advisory](https://github.com/JaatrovyKnedlicek/MC-Server-Manager-Windows/security/advisories/new)
- Or open a [GitHub Security Issue](https://github.com/JaatrovyKnedlicek/MC-Server-Manager-Windows/issues) marked as sensitive

**What to Include:**
1. Description of the vulnerability
2. Steps to reproduce (if applicable)
3. Potential impact
4. Suggested fix (if you have one)

### Response Timeline

We aim to:
- **Acknowledge** your report within 48 hours
- **Provide initial assessment** within 1 week
- **Release a fix** or security update as soon as possible
- **Credit you** in the release notes (unless you prefer anonymity)

### Security Updates

Security updates will be released:
- As soon as feasible after fix completion
- With clear security advisories on GitHub
- With migration guidance in release notes

## Security Considerations

### Application Security

This application:

✅ **Does:**
- Run Minecraft servers in isolated processes
- Store configuration in local JSON files (no cloud/external services)
- Support standard Java security mechanisms
- Allow EULA acceptance verification
- Use .NET standard library only (no questionable third-party dependencies)

⚠️ **Important Notes:**
- Servers run with full system permissions (be cautious who has access)
- Configuration files contain sensitive data (backup securely)
- Console output not captured (use server logs for monitoring)
- No built-in authentication/authorization system

### Data Security

**Stored Data:**
- Server configurations in `servers/` folder
- World data in server directories
- Logs in `logs/` subdirectories

**Recommendations:**
- Keep `servers/` folder on encrypted drive (for production)
- Regular backups to secure location
- Restrict folder permissions to trusted users
- Don't expose server data to untrusted networks

### Process Security

**Server Execution:**
- Servers run in detached processes
- No console input/output redirection (safer isolation)
- Use Java security manager if additional hardening needed
- Monitor resource usage (CPU, RAM, Disk)

**Recommendations:**
- Run manager as standard user (not Administrator)
- Keep Java updated for security patches
- Monitor server processes (Windows Task Manager or tools)

### Network Security

**Connection Security:**
- No direct remote management (local only)
- Server listens on configured port (default 25565)
- Use firewall to restrict access
- Disable `online-mode` only for trusted LAN

**Recommendations:**
- Use firewall rules to restrict port access
- Keep servers behind NAT if on public network
- Use VPN for remote access (don't expose directly)
- Monitor for suspicious connections

### Dependency Security

**Current Dependencies:**
- **.NET 9 Runtime** - Microsoft maintained
- **Java Runtime** - Oracle maintained
- **Paper Server** - Community maintained

**No external NuGet packages** (intentional design choice)

**Recommendations:**
- Keep .NET 9 runtime updated
- Keep Java 17+ updated
- Keep Minecraft Paper updated
- Monitor GitHub Security Advisories

## Known Limitations

1. **No Authentication** - Anyone with access to manager can control servers
2. **No Encryption** - Configuration files stored in plain JSON
3. **Local Only** - No remote management capabilities
4. **Process Isolation** - Relies on OS process security
5. **No Audit Logging** - No built-in action logging

## Responsible Disclosure

We follow responsible disclosure practices:
- Private vulnerability reporting
- Coordinated release with security fixes
- Public advisories after patches available
- Appreciation for security researchers

## Security Checklist for Administrators

Use this checklist when deploying MC Server Manager:

- [ ] Install on Windows 10 or later (latest security patches)
- [ ] Keep .NET 9 runtime updated
- [ ] Keep Java 17+ updated
- [ ] Store `servers/` on encrypted drive
- [ ] Restrict `servers/` folder permissions
- [ ] Regular backups of world data
- [ ] Use strong passwords for online-mode servers
- [ ] Monitor server resource usage
- [ ] Use firewall to restrict port access
- [ ] Keep anti-malware software current
- [ ] Don't run manager as Administrator unless necessary
- [ ] Review server logs regularly

## Security Best Practices

### For Users

1. **Keep Software Updated**
   - .NET 9 updates
   - Java updates
   - Windows updates

2. **Access Control**
   - Don't run as Administrator
   - Use standard user account
   - Restrict folder permissions

3. **Data Protection**
   - Regular backups
   - Encrypted storage
   - Secure backup location

4. **Monitoring**
   - Check server logs
   - Monitor resource usage
   - Watch for errors

### For Server Operators

1. **Server Configuration**
   - Keep `online-mode=true` for public servers
   - Use strong RCON password (if enabled)
   - Whitelist players if needed

2. **Access Management**
   - Limit player count
   - Monitor join/leave events
   - Keep player whitelist

3. **Resource Management**
   - Allocate appropriate RAM
   - Monitor disk space
   - Set view-distance appropriately

4. **Regular Maintenance**
   - Weekly backups
   - Monthly log review
   - Periodic server restart

## Security Resources

- [OWASP Security Guidelines](https://owasp.org/)
- [CWE Top 25](https://cwe.mitre.org/top25/)
- [Microsoft Security Best Practices](https://docs.microsoft.com/security/)
- [Java Security Documentation](https://docs.oracle.com/javase/17/security/)
- [Minecraft Security](https://www.minecraft.net/en-us/article/minecraft-security)

## Version History

| Version | Date | Security Updates |
|---------|------|------------------|
| 3.0+ | 2026+ | Policy established |

## Contact

For security inquiries:
- **GitHub Security Advisory:** Create on repository
- **Email:** Contact repository maintainer
- **Discussion:** Private discussion through GitHub

---

**Last Updated:** 2025

**Thank you** for helping keep this project secure! 🔒
