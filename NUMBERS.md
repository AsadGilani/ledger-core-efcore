# NUMBERS.md — Constants and Design Choices

1. **Interest Rate: `0.0004` (0.04% per day)**
   - *Why this value:* Explicitly mandated by the problem specification. Not half because financial interest rates are deterministic contractual parameters, not tunable hyperparameters.

2. **Overdraft Fee Amount: `25.00` AED**
   - *Why this value:* Mandated by non-negotiable rules. Fixed fee per account per negative closing balance day.

3. **Currency Decimal Precision:**
   - **AED:** `2` decimal places (scale factor `100`). Chosen because AED minor currency unit is fils (1/100).
   - **BHD:** `3` decimal places (scale factor `1000`). Chosen because BHD minor currency unit is fils (1/1000).
   - *Why not half:* Currency precision is governed by ISO 4217 currency standards. Halving precision would truncate valid fractional currency units (e.g., BHD fils).

4. **Window Duration: `6` Days (Day 1 through Day 6)**
   - *Why this value:* Fixed simulation window defined in the prompt specification.