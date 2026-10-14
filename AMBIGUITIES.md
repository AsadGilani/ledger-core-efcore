# AMBIGUITIES.md — Ambiguities Found and Resolved

1. **Back-dated Entries & Value Dates vs. Processing Dates (E7 & E9):**
   - *Ambiguity:* E7 is received on Day 5 but has `ValueDate` Day 2. E9 is received on Day 6 but reverses E7 with `ValueDate` Day 2. How do value dates affect daily closing ledger balances and overdraft fee assessments across days?
   - *Resolution:* Closing ledger balance for Day $N$ is defined as the sum of all entries where `ValueDate <= Day N`. For daily closing reports printed per day, we evaluate entries matching the value date condition.

2. **Authorization Hold Release on Settlement (E5):**
   - *Ambiguity:* When Auth-A (hold AED 200.00) settles for AED 185.00 (E5), what happens to the remaining AED 15.00 hold?
   - *Resolution:* Settlement of an authorization releases the entire active hold associated with that authorization ID and replaces it with the settled debit entry. The remaining hold is fully released back to available balance.

3. **Unmatched Settlement (E6):**
   - *Ambiguity:* E6 settles Auth-Z for AED 180.00, but Auth-Z has no preceding authorization event.
   - *Resolution:* Per acceptance criteria rules, any settlement referencing an authorization ID not present in the ledger must be rejected, funds must not leave the account, and an audit error must be logged.

4. **Interest Accrual Precision and Exact Summation:**
   - *Ambiguity:* How to distribute daily 0.04% interest so that rounded daily accruals sum exactly to the capitalized total without remainder loss?
   - *Resolution:* Calculate daily interest on positive closing ledger balance, round each day using `MidpointRounding.AwayFromZero`, accumulate the sum, and apply any rounding discrepancy (+/- 0.01) to the final day's capitalized credit entry.