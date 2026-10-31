using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using QuestTennisTrainer.TrainingModes.BallMachine;

namespace QuestTennisTrainer.Core
{
    /// <summary>
    /// Result descriptor for local TOON telemetry persistence and SHA-256 integrity verification.
    /// </summary>
    [Serializable]
    public struct PersistedSessionManifest
    {
        public string SessionId;
        public string FilePath;
        public string Sha256Hex;
        public int TotalStrokes;
        public float AverageSpeedKmh;
        public float SweetSpotHitRatePercent;
        public AdaptiveDifficultyTier HighestAdaptiveTier;
        public string ToonPayload;
    }

    /// <summary>
    /// Session 15: Local-first TOON stroke telemetry persistence & GCP Cloud Storage / Firestore envelope builder.
    /// Saves compact tabular session files with deterministic SHA-256 integrity headers and formats cloud backup payloads.
    /// </summary>
    public static class SessionTelemetryPersistence
    {
        public const string IntegrityHeaderPrefix = "# sha256:";

        /// <summary>
        /// Computes a lowercase hexadecimal SHA-256 digest of a UTF-8 telemetry payload.
        /// </summary>
        public static string ComputeSha256Hex(string payload)
        {
            string normalized = (payload ?? string.Empty).Replace("\r\n", "\n").TrimEnd();
            byte[] bytes = Encoding.UTF8.GetBytes(normalized);
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                StringBuilder sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                }
                return sb.ToString();
            }
        }

        /// <summary>
        /// Persists a SwingCoachAnalyzer session to disk as a verifiable `.toon` file with a SHA-256 header.
        /// </summary>
        public static PersistedSessionManifest SaveSessionToDirectory(
            string directoryPath,
            string sessionId,
            SwingCoachAnalyzer coach,
            AdaptiveDifficultyTier highestTier = AdaptiveDifficultyTier.Tier2_BaselineRally)
        {
            if (coach == null)
            {
                throw new ArgumentNullException(nameof(coach));
            }

            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new ArgumentException("Directory path must not be empty.", nameof(directoryPath));
            }

            string safeSessionId = string.IsNullOrWhiteSpace(sessionId) ? "quest3-session" : sessionId.Trim();
            Directory.CreateDirectory(directoryPath);

            SessionCoachingSummary summary = coach.ComputeSessionSummary();
            string toonBody = coach.ExportSessionToCompactTable().Replace("\r\n", "\n").TrimEnd();

            string filePath = Path.Combine(directoryPath, $"{safeSessionId}.toon");
            string fileContent = FormatSignedToonDocument(toonBody, out string sha256Hex);
            File.WriteAllText(filePath, fileContent, Encoding.UTF8);

            return new PersistedSessionManifest
            {
                SessionId = safeSessionId,
                FilePath = filePath,
                Sha256Hex = sha256Hex,
                TotalStrokes = summary.TotalShots,
                AverageSpeedKmh = summary.AverageSpeedKmh,
                SweetSpotHitRatePercent = summary.SweetSpotHitRatePercent,
                HighestAdaptiveTier = highestTier,
                ToonPayload = toonBody
            };
        }

        /// <summary>
        /// Formats a TOON telemetry payload with a deterministic `# sha256:<hex>` integrity header.
        /// </summary>
        public static string FormatSignedToonDocument(string toonBody, out string sha256Hex)
        {
            string normalizedBody = (toonBody ?? string.Empty).Replace("\r\n", "\n").TrimEnd();
            sha256Hex = ComputeSha256Hex(normalizedBody);
            return $"{IntegrityHeaderPrefix}{sha256Hex}\n{normalizedBody}\n";
        }

        /// <summary>
        /// Verifies the embedded SHA-256 integrity header of a signed `.toon` document string.
        /// </summary>
        public static bool TryVerifySignedToonDocument(
            string signedDocumentContent,
            out string verifiedToonPayload,
            out string expectedSha256Hex)
        {
            verifiedToonPayload = string.Empty;
            expectedSha256Hex = string.Empty;

            if (string.IsNullOrWhiteSpace(signedDocumentContent))
            {
                return false;
            }

            string raw = signedDocumentContent.Replace("\r\n", "\n");
            int firstNewline = raw.IndexOf('\n');
            if (firstNewline <= 0)
            {
                return false;
            }

            string headerLine = raw.Substring(0, firstNewline).Trim();
            if (!headerLine.StartsWith(IntegrityHeaderPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            expectedSha256Hex = headerLine.Substring(IntegrityHeaderPrefix.Length).Trim();
            string body = raw.Substring(firstNewline + 1).TrimEnd();
            string actualSha256Hex = ComputeSha256Hex(body);

            if (!string.Equals(expectedSha256Hex, actualSha256Hex, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            verifiedToonPayload = body;
            return true;
        }

        /// <summary>
        /// Loads a persisted `.toon` session file from disk and verifies its embedded SHA-256 integrity digest.
        /// </summary>
        public static bool TryLoadAndVerifySessionFile(
            string filePath,
            out string verifiedToonPayload,
            out string expectedSha256Hex)
        {
            verifiedToonPayload = string.Empty;
            expectedSha256Hex = string.Empty;

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return false;
            }

            string raw = File.ReadAllText(filePath, Encoding.UTF8);
            return TryVerifySignedToonDocument(raw, out verifiedToonPayload, out expectedSha256Hex);
        }

        /// <summary>
        /// Builds a compact JSON envelope for optional upload to GCP Cloud Storage or Cloud Firestore.
        /// </summary>
        public static string BuildGcpTelemetryEnvelopeJson(
            string sessionId,
            SwingCoachAnalyzer coach,
            AdaptiveDifficultyTier highestTier = AdaptiveDifficultyTier.Tier2_BaselineRally)
        {
            if (coach == null)
            {
                throw new ArgumentNullException(nameof(coach));
            }

            SessionCoachingSummary summary = coach.ComputeSessionSummary();
            string toonBody = coach.ExportSessionToCompactTable().Replace("\r\n", "\n").TrimEnd();
            string sha256Hex = ComputeSha256Hex(toonBody);
            string escapedToon = toonBody.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");

            return string.Format(
                CultureInfo.InvariantCulture,
                "{{\"sessionId\":\"{0}\",\"hardware\":\"Meta Quest 3\",\"physicsHz\":120,\"totalShots\":{1},\"avgSpeedKmh\":{2:F1},\"avgSpinRpm\":{3:F0},\"sweetSpotHitRatePct\":{4:F1},\"highestAdaptiveTier\":\"{5}\",\"sha256\":\"{6}\",\"toonTelemetry\":\"{7}\"}}",
                sessionId ?? "quest3-session",
                summary.TotalShots,
                summary.AverageSpeedKmh,
                summary.AverageSpinRpm,
                summary.SweetSpotHitRatePercent,
                highestTier,
                sha256Hex,
                escapedToon);
        }
    }
}
