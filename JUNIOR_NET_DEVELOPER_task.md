# Interview Home Work Assignment

## Table of Contents
- [Summary](#summary)
- [Functional Requirements](#functional-requirements)
  - [1. Power Plant Schema](#1-power-plant-schema)
  - [2. GET API Endpoint](#2-get-api-endpoint)
  - [3. POST API Endpoint](#3-post-api-endpoint)
  - [4. Filtering for GET](#4-filtering-for-get)
- [Non-Functional Requirements](#non-functional-requirements)
- [Bonus Requirements (nice to have)](#bonus-requirements-nice-to-have)
- [Interview Process Notes](#interview-process-notes)

---

## Summary
Implement a system that can store or retrieve a list of power plants. Use best practices and coding standards as you think best.

---

## Functional Requirements

### 1. Power Plant Schema
A power plant should be defined by the following fields:

| Name       | Type               | Required | Example              |
|------------|--------------------|----------|----------------------|
| Owner      | text               | true     | `Vardenis Pavardenis` |
| Power      | number (decimal)   | true     | `9.3`                |
| Valid From | date               | true     | `2020-01-01`         |
| Valid To   | date (nullable)    | false    | `2025-01-01`         |

### 2. GET API Endpoint
Create a **GET** API endpoint that retrieves all stored power plants as a JSON response.

**Response example:**
```json
{
  "powerPlants": [
    {
      "owner": "Vardenis Pavardenis",
      "power": 9.3,
      "validFrom": "2020-01-01",
      "validTo": "2025-01-01"
    },
    {
      "owner": "Jonas Jonaitis",
      "power": 5.7,
      "validFrom": "2021-06-15",
      "validTo": "2026-06-15"
    },
    {
      "owner": "Ona Petraitė",
      "power": 12.5,
      "validFrom": "2019-09-10",
      "validTo": null
    }
  ]
}
```

### 3. POST API Endpoint
Create a **POST** API endpoint that adds a new power plant to the stored list.

**Validation rules:**

1. **Missing required field**  
   - **Given** a required field is missing  
   - **When** a POST request is submitted  
   - **Then** return **Bad Request** (HTTP **400**).

2. **Power bounds**  
   - **Given** `power` is less than **0** or greater than **200**  
   - **When** a POST request is submitted  
   - **Then** return **Bad Request** (HTTP **400**).

3. **Owner format**  
   - **Given** `owner` does not consist of two words (text-only characters) separated by a whitespace  
   - **When** a POST request is submitted  
   - **Then** return **Bad Request** (HTTP **400**).

4. **Valid data**  
   - **Given** all power plant data is valid  
   - **When** a POST request is submitted  
   - **Then** return **Created** (HTTP **201**) with the created resource.

**Request example:**
```json
{
  "owner": "Ona Petraitė",
  "power": 12.5,
  "validFrom": "2019-09-10",
  "validTo": null
}
```

### 4. Filtering for GET
- **Given** a query parameter `owner` is provided (e.g., `?owner=ona`)  
- **When** a GET request is submitted  
- **Then** return only power plants whose `owner` field contains the specified parameter value.

---

## Non-Functional Requirements
1. Use the latest stable version of .NET — [Download .NET](https://dotnet.microsoft.com/en-us/download).  
2. Code should be stored in a git repository reachable by a shared URL, e.g. <https://github.com/>.  
3. Use a relational database for persistence.

---

## Bonus Requirements (nice to have)
1. Use **EF Core**.  
2. Add a **unit test** for the validation logic of the POST endpoint.  
3. Add **paging** to the GET endpoint.  
4. Improve filtering to work with **accented characters**, e.g., filter `"petraite"` should match `"Ona Petraitė"`.  
5. **Bad Request** responses (POST endpoint) should include **error descriptions** of what went wrong.

---

## Interview Process Notes
- During the interview we review the completed homework assignment and ask questions / give feedback about the implementation.  
- An additional on-the-spot requirement can be added to the assignment during the interview so the interviewee implements on-the-spot changes.
