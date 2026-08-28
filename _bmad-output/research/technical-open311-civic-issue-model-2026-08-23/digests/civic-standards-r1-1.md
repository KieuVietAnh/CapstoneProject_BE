# Digest: Open311 / 311 civic issue model

Accessed: 2026-08-23

## Findings

1. **Claim:** Open311 GeoReport v2's main resources are service types and service requests. `POST /requests` creates a service request. A `service_request_id` is the unique ID of the created request and is used to retrieve that request's current status.
   - source: https://wiki.open311.org/GeoReport_v2/
   - publisher: Open311
   - pub_date: n.d. (spec marked Stable)
   - confidence: high
   - class: standard/schema

2. **Claim:** Open311's token is a temporary tracking resource for asynchronous/batch creation, not an issue ID: a batch service type returns a token first, and `GET /tokens/{token}` later resolves it to the `service_request_id`. Realtime types return the request ID immediately; blackbox types return no ID.
   - source: https://wiki.open311.org/GeoReport_v2/
   - publisher: Open311
   - pub_date: n.d. (spec marked Stable)
   - confidence: high
   - class: standard/schema

3. **Claim:** Open311 v2 status is deliberately minimal: `open` means reported and `closed` means resolved; optional `status_notes` explains the current state. Yet the official example shows a request as `closed` with `status_notes="Duplicate request."` while another same-type/same-address request remains open. Therefore a closed request cannot safely be equated with resolution of the underlying real-world condition.
   - source: https://wiki.open311.org/GeoReport_v2/
   - publisher: Open311
   - pub_date: n.d. (spec marked Stable)
   - confidence: high
   - class: standard/semantic-ambiguity

4. **Claim:** GeoReport v2 can represent multiple service-request records that appear to concern the same condition (the duplicate example), but its documented response schema has no standard `incident_id`, `parent_request_id`, `duplicate_of`, canonical-issue, or subscription relation. Thus one-to-many Report→Issue is not a standardized Open311 v2 concept; it is an implementation concern.
   - source: https://wiki.open311.org/GeoReport_v2/
   - publisher: Open311
   - pub_date: n.d. (spec marked Stable)
   - confidence: high for schema absence; medium for interpretation
   - class: standard/gap

5. **Claim:** NYC311 defines a service request as asking the City for assistance, inspection, or action on a problem. It gives the submitter an SR number and SLA, sends updates when the assigned agency acts, and allows anyone to subscribe/follow an existing SR, including SRs submitted by others. Follow is therefore a watcher/subscription relationship to one SR, not evidence of a merged incident record.
   - source: https://portal.311.nyc.gov/article/?kanumber=KA-03116
   - publisher: NYC311, City of New York
   - pub_date: n.d.
   - confidence: high
   - class: municipal/product-behavior

6. **Claim:** NYC publicly exposes duplicate handling: an SR can say the issue was previously reported by another customer and that the original complaint is being addressed. The duplicate SR retains its own SR number. The public detail page reviewed does not expose the original SR number, so a public linked-request relation was not found.
   - source: https://portal.311.nyc.gov/sr-details/?id=849ef0c5-a62f-ec11-b76a-2818785c9413
   - publisher: NYC311, City of New York
   - pub_date: n.d. (record concerned 2021-10-18 in indexed metadata)
   - confidence: high for duplicate behavior; medium for public-link absence
   - class: municipal/duplicate

7. **Claim:** NYC's official 311 dataset is row-per-service-request: `unique_key` uniquely identifies an SR; `created_date`, `closed_date`, `status`, agency, resolution, and channel are SR fields. `due_date` is the date the responding agency is expected to update the SR and is based on complaint type and internal SLA. “Incident” fields are location descriptors, not an incident identity.
   - source: https://data.cityofnewyork.us/api/views/erm2-nwe9
   - publisher: NYC Open Data / NYC311
   - pub_date: 2025-12-23 (metadata publication timestamp; dataset created 2011-10-10)
   - confidence: high
   - class: municipal/schema-SLA

8. **Claim:** DC311 independently confirms request-centric operations: duplicate detection is an explicit platform feature; the confirmation number tracks one SR; only the servicing agency closes it. An official OUC response says duplicate detection is configurable by agency and may use request type, location, and timeframe; misrouting can change request type and assign a new SLA; each SR workflow is unique and the response includes its expected completion date.
   - sources:
     - https://ouc.dc.gov/page/dc-311-mobile-app
     - https://ouc.dc.gov/page/311-city-services
     - https://resolutions.anc.dc.gov/AttachmentsFiles/19/10012020%20-%20Response%20to%20ANC%204C%20Audit%20Request%20and%20Process%20Improvements%20for%20OUC_TM_20201230051446PM.pdf
   - publisher: District of Columbia Office of Unified Communications
   - pub_date: n.d. for web pages; 2020-10-01 for official response letter
   - confidence: high
   - class: municipal/workflow-SLA

## Decision synthesis

- Use three concepts if the product must preserve citizen participation and operational deduplication: **Report/Submission** (immutable person-channel observation), **Issue/Incident** (optional canonical real-world condition, one-to-many reports), and **Service Request/Case** (agency-owned actionable workflow/SLA unit). Map Open311 `service_request_id` to Service Request/Case, not directly to Report and not necessarily to Incident.
- If only two concepts are affordable, keep **Report/Request** as the externally visible, SLA-bearing record and make duplicate/follow relationships explicit. Do not call it Incident unless the system guarantees one canonical condition with multiple reporters.
- Keep lifecycle fields separate: `request_status` (including duplicate/merged/cancelled) versus `issue_status` (condition unresolved/resolved). Open311's closed-duplicate example shows why one shared status is lossy.

## Not found / contradictions / leads

- Not found in Open311 GeoReport v2: a canonical underlying issue entity, request-to-incident link, duplicate-of ID, parent/child request relationship, or follower/subscriber schema.
- Not found in NYC's public 311 dataset/detail page: a field exposing the original SR for a duplicate. This does not prove the agency backend lacks such a link.
- DC documents duplicate detection, but the reviewed public sources do not state whether duplicates are linked to a canonical SR, prevented, or merely flagged.
- Contradiction: Open311 defines `closed` as resolved, but its own example closes a duplicate while another request remains open; NYC similarly marks a duplicate while saying the original is still being addressed.
- Lead: inspect a specific municipal vendor/backend schema (Salesforce/SeeClickFix/Granicus/Cityworks) if the design needs exact merge/link/audit semantics rather than API-level compatibility.
- Lead: NYC Council Int 0744-2024 proposed barring closure solely for duplication but was filed at end of session, so it is policy debate—not current binding behavior.

## Sources used (6)

1. Open311 GeoReport v2 — https://wiki.open311.org/GeoReport_v2/
2. NYC311 Service Requests — https://portal.311.nyc.gov/article/?kanumber=KA-03116
3. NYC311 duplicate SR detail — https://portal.311.nyc.gov/sr-details/?id=849ef0c5-a62f-ec11-b76a-2818785c9413
4. NYC Open Data 311 schema — https://data.cityofnewyork.us/api/views/erm2-nwe9
5. DC OUC 311 web pages — https://ouc.dc.gov/page/dc-311-mobile-app and https://ouc.dc.gov/page/311-city-services
6. DC OUC response letter — https://resolutions.anc.dc.gov/AttachmentsFiles/19/10012020%20-%20Response%20to%20ANC%204C%20Audit%20Request%20and%20Process%20Improvements%20for%20OUC_TM_20201230051446PM.pdf
