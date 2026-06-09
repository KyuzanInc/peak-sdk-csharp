// Reference consumer of KyuzanInc.Peak.Sdk for the Unity IL2CPP AOT smoke.
// Modeled on the peak-monorepo PeakSdkDemo.cs (not vendored). No UniTask:
// async void MonoBehaviour callbacks with explicit try/catch (the one place
// async void is acceptable). See docs/superpowers/specs/
// 2026-06-09-unity-reference-example-otp-login-design.md.
using System;
using System.Threading.Tasks;
using KyuzanInc.Peak.Sdk;
using KyuzanInc.Peak.Sdk.Models;
using UnityEngine;

namespace Peak.Example
{
    public sealed class PeakExampleDemo : MonoBehaviour
    {
        // chainType valid domain is exactly {"evm","solana"} (spec D6); "ETHEREUM" throws.
        private const string ChainTypeEvm = "evm";
        private const string KeyFormatHex = "HEXADECIMAL";

        private enum State { Uninitialized, Idle, AwaitingOtp, Authenticated }

        // apiUrl is non-secret, so a SerializeField is fine. The projectApiKey is
        // NEVER serialized (it would land in the committed scene YAML) — runtime entry
        // only, with an env-var fallback for Standalone convenience (spec D9).
        [SerializeField] private string apiUrl = "https://api.peak.xyz";

        private PeakClient _client;
        private AuthenticatedPeakClient _authed;
        private State _state = State.Uninitialized;
        private bool _busy;

        private string _apiKey = "";
        private string _email = "";
        private string _otpCode = "";
        private string _pendingOtpId = "";
        private string _importKeyHex = "";   // TEST keys only
        private string _address = "";        // set from List Addresses; gates Export
        private string _log = "";

        private void Awake()
        {
            // Env-var fallback so a Standalone smoke can avoid typing the key each run.
            var fromEnv = Environment.GetEnvironmentVariable("PEAK_PROJECT_API_KEY");
            if (!string.IsNullOrEmpty(fromEnv)) _apiKey = fromEnv;
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 460, 600));
            GUILayout.Label($"Peak SDK — Unity reference example   [{_state}]");
            if (_busy) GUILayout.Label("working…");

            switch (_state)
            {
                case State.Uninitialized: DrawUninitialized(); break;
                case State.Idle: DrawIdle(); break;
                case State.AwaitingOtp: DrawAwaitingOtp(); break;
                case State.Authenticated: DrawAuthenticated(); break;
            }

            GUILayout.Label("Log:");
            GUILayout.TextArea(_log, GUILayout.Height(180));
            GUILayout.EndArea();
        }

        private void DrawUninitialized()
        {
            GUILayout.Label("API URL:");
            apiUrl = GUILayout.TextField(apiUrl);
            GUILayout.Label("Project API key (TEST env — not stored in the scene):");
            _apiKey = GUILayout.PasswordField(_apiKey, '*');
            if (GUILayout.Button("Initialize"))
            {
                try
                {
                    // The (apiUrl, projectApiKey) overload leaves LoggerFactory null
                    // -> NullLogger, so the SDK's Debug request-body logging is inert (D9).
                    _client = PeakClient.Initialize(apiUrl, _apiKey);
                    _state = State.Idle;
                    Append("initialized");
                }
                catch (PeakError ex) { Fail(ex); }
            }
        }

        private void DrawIdle()
        {
            GUILayout.Label("Email:");
            _email = GUILayout.TextField(_email);
            if (Button("Send OTP")) OnSendOtp();
        }

        private void DrawAwaitingOtp()
        {
            GUILayout.Label("OTP code:");
            _otpCode = GUILayout.TextField(_otpCode);
            if (Button("Complete Login")) OnCompleteLogin();
        }

        private void DrawAuthenticated()
        {
            if (Button("List Accounts")) OnListAccounts();
            if (Button("List Addresses (uses first account)")) OnListAddresses();
            GUILayout.Label("Private key to import (TEST only):");
            _importKeyHex = GUILayout.TextField(_importKeyHex);
            if (Button("Import Private Key")) OnImport();
            // Export needs an address (spec §4.3 ordering precondition).
            GUI.enabled = !string.IsNullOrEmpty(_address) && !_busy;
            if (GUILayout.Button("Export Private Key")) OnExport();
            GUI.enabled = !_busy;
            if (Button("Logout")) { _client.Logout(); _authed = null; _state = State.Idle; Append("logged out"); }
        }

        // --- async void handlers: try/catch, no ConfigureAwait(false), _busy guard (D7) ---

        private async void OnSendOtp()
        {
            if (!Begin()) return;
            try
            {
                InitOtpLoginResponse? result = await _client.InitOtpLoginAsync(_email);
                _pendingOtpId = result?.OtpId ?? "";
                if (string.IsNullOrEmpty(_pendingOtpId)) { Append("no otpId returned"); return; }
                _state = State.AwaitingOtp;
                Append("OTP sent");
            }
            catch (PeakError ex) { Fail(ex); }
            catch (Exception ex) { Fail(ex); }
            finally { End(); }
        }

        private async void OnCompleteLogin()
        {
            if (!Begin()) return;
            try
            {
                await _client.CompleteOtpLoginAsync(_email, _pendingOtpId, _otpCode);
                _authed = _client.Authenticate();
                _state = State.Authenticated;
                Append("authenticated");
            }
            catch (PeakError ex) { Fail(ex); }
            catch (Exception ex) { Fail(ex); }
            finally { End(); }
        }

        private async void OnListAccounts()
        {
            if (!Begin()) return;
            try
            {
                AccountResponse[] accounts = await _authed.ListAccountsAsync();
                Append($"accounts: {accounts.Length}");
                foreach (var a in accounts) Append($"  acct {Redact(a.Id)} idx={a.AccountIndex}");
            }
            catch (PeakError ex) { Fail(ex); }
            catch (Exception ex) { Fail(ex); }
            finally { End(); }
        }

        private async void OnListAddresses()
        {
            if (!Begin()) return;
            try
            {
                AccountResponse[] accounts = await _authed.ListAccountsAsync();
                if (accounts.Length == 0) { Append("no accounts"); return; }
                AccountAddressResponse[] addrs = await _authed.ListAccountAddressesAsync(accounts[0].Id ?? "");
                Append($"addresses: {addrs.Length}");
                if (addrs.Length > 0) { _address = addrs[0].Address ?? ""; Append($"  using {Redact(_address)}"); }
            }
            catch (PeakError ex) { Fail(ex); }
            catch (Exception ex) { Fail(ex); }
            finally { End(); }
        }

        private async void OnImport()
        {
            if (!Begin()) return;
            try
            {
                InitImportPrivateKeyResult init = await _authed.InitImportPrivateKeyAsync();
                string bundle = PeakCrypto.EncryptPrivateKeyToBundle(new PeakCrypto.EncryptPrivateKeyToBundleParams
                {
                    PrivateKey = _importKeyHex,
                    ImportBundle = init.ImportBundle,
                    OrganizationId = init.OrganizationId,
                    UserId = init.UserId,
                    KeyFormat = KeyFormatHex,
                });
                await _authed.CompleteImportPrivateKeyAsync(bundle, ChainTypeEvm);
                Append("import ok");
            }
            catch (PeakError ex) { Fail(ex); }
            catch (Exception ex) { Fail(ex); }
            finally { End(); }
        }

        private async void OnExport()
        {
            if (!Begin()) return;
            try
            {
                PeakCrypto.KeyPair target = PeakCrypto.GenerateP256KeyPair();
                ExportPrivateKeyResult exp = await _authed.ExportPrivateKeyAsync(_address, target.PublicKey);
                string key = PeakCrypto.DecryptExportBundle(new PeakCrypto.DecryptExportBundleParams
                {
                    ExportBundle = exp.ExportBundle,
                    EmbeddedKey = target.PrivateKey,
                    OrganizationId = exp.OrganizationId,
                    KeyFormat = KeyFormatHex,
                    ReturnMnemonic = false,
                });
                // NEVER log the raw key (spec D9) — only a redacted marker.
                Append($"export ok: {Redact(key)}");
            }
            catch (PeakError ex) { Fail(ex); }
            catch (Exception ex) { Fail(ex); }
            finally { End(); }
        }

        // --- helpers ---
        private bool Button(string label) => GUILayout.Button(label) && !_busy;
        private bool Begin() { if (_busy) return false; _busy = true; return true; }
        private void End() { _busy = false; }
        private void Append(string line) => _log = line + "\n" + _log;
        private void Fail(Exception ex)
        {
            string code = ex is PeakError pe ? pe.Code : "ERR";
            // Log code + message only; never ApiResponse.RawResponseBody (may echo secrets, D9).
            Append($"{code}: {ex.Message}");
            Debug.LogError($"[PeakExample] {code}: {ex.Message}");
        }
        private static string Redact(string s)
            => string.IsNullOrEmpty(s) ? "(empty)" : (s.Length <= 6 ? "…" : s.Substring(0, 6) + "…(redacted)");
    }
}
