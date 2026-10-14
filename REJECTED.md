# REJECTED.md — Rejected Acceptance Criteria & Abandoned Approaches

## Rejected Acceptance Criteria

1. **"The three BHD instalments in E10 must each be BHD 3.334."**
   - *Reason:* BHD 10.000 divided into 3 equal instalments mathematically yields 3.3333... Rounded to 3 decimal places, each instalment is 3.333. If each is 3.334, their sum is 10.002, violating the total amount of E10 (10.000 BHD). The final installment must absorb the rounding remainder to sum exactly to 10.000.

2. **"If the rounded daily interest accruals do not sum to the capitalized total, the remainder is discarded."**
   - *Reason:* Directly contradicts the non-negotiable rule: *"The rounded daily accruals must sum exactly to the capitalized total."* Discarding the remainder creates accounting drift.

3. **"E7 causes exactly one overdraft fee to be assessed, on Day 2."**
   - *Reason:* E7 occurs on Day 5 with a back-dated `ValueDate` of Day 2. In an append-only event stream, if ledger balances are evaluated chronologically as days close in real-time, Day 2 closed with a positive balance (+250.00). Back-dating entries requires defining whether past days are retroactively re-closed. We reject retroactive fee mutation unless explicitly specified.

---

## Abandoned Approaches Mid-Build

1. **Mutable Balance Cache Table:**
   - *Approach:* Storing running balance states in a mutable hash map updated in place per event.
   - *Why abandoned:* Violates the core non-negotiable invariant: *"The ledger is append-only. No event record is ever mutated or deleted."* Replaced with EF Core LINQ queries projecting balances dynamically from immutable ledger entries.

2. **Floating-Point Arithmetic for Interest Accruals:**
   - *Approach:* Using standard C# `double` or `float` for daily 0.04% interest calculations.
   - *Why abandoned:* Floating-point rounding errors accumulated across multi-day decimal truncations, causing cent discrepancies when summing daily accruals. Replaced entirely with `decimal` data types and strict currency rounding.