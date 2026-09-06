import sys
import re

# Copy the tokenize_sql function directly
def tokenize_sql(sql_text):
    tokens = []
    i = 0
    n = len(sql_text)
    code_start = 0

    while i < n:
        if sql_text[i] == "'":
            if i > code_start:
                tokens.append(('code', sql_text[code_start:i]))
            str_start = i
            i += 1
            while i < n:
                if sql_text[i] == "'":
                    if i + 1 < n and sql_text[i + 1] == "'":
                        i += 2
                    else:
                        i += 1
                        break
                elif sql_text[i] == '\\':
                    i += 2
                else:
                    i += 1
            tokens.append(('string', sql_text[str_start:i]))
            code_start = i
        elif sql_text[i] == '"':
            if i > code_start:
                tokens.append(('code', sql_text[code_start:i]))
            str_start = i
            i += 1
            while i < n:
                if sql_text[i] == '"':
                    if i + 1 < n and sql_text[i + 1] == '"':
                        i += 2
                    else:
                        i += 1
                        break
                elif sql_text[i] == '\\':
                    i += 2
                else:
                    i += 1
            tokens.append(('string_dq', sql_text[str_start:i]))
            code_start = i
        elif sql_text[i] == '`':
            if i > code_start:
                tokens.append(('code', sql_text[code_start:i]))
            str_start = i
            i += 1
            while i < n:
                if sql_text[i] == '`':
                    if i + 1 < n and sql_text[i + 1] == '`':
                        i += 2
                    else:
                        i += 1
                        break
                else:
                    i += 1
            tokens.append(('identifier', sql_text[str_start:i]))
            code_start = i
        elif sql_text[i:i+2] == '--':
            if i > code_start:
                tokens.append(('code', sql_text[code_start:i]))
            cmt_start = i
            i = sql_text.find('\n', i)
            if i == -1:
                i = n
            else:
                i += 1
            tokens.append(('comment_line', sql_text[cmt_start:i]))
            code_start = i
        elif sql_text[i:i+2] == '/*':
            if i > code_start:
                tokens.append(('code', sql_text[code_start:i]))
            cmt_start = i
            end_idx = sql_text.find('*/', i + 2)
            if end_idx == -1:
                i = n
            else:
                i = end_idx + 2
            tokens.append(('comment_block', sql_text[cmt_start:i]))
            code_start = i
        else:
            i += 1

    if code_start < n:
        tokens.append(('code', sql_text[code_start:n]))

    return tokens

# Test 1: Nested BEGIN without label
sql1 = "CREATE PROCEDURE p()\nBEGIN\n  BEGIN SELECT 1; END;\n  SELECT 2;\nEND;"

tokens1 = tokenize_sql(sql1)
print('=== Test 1: Nested BEGIN without label ===')
for i, tok in enumerate(tokens1):
    print(f'{i}: {tok}')

# Test 2: END with backtick label in same token
sql2 = "END`outer_block`;"

tokens2 = tokenize_sql(sql2)
print()
print('=== Test 2: END with backtick label in same token ===')
for i, tok in enumerate(tokens2):
    print(f'{i}: {tok}')

# Test 3: END with backtick label - full procedure
sql3 = "CREATE PROCEDURE outer_block()\nouter_block:BEGIN\n  SELECT 1;\nEND`outer_block`;"

tokens3 = tokenize_sql(sql3)
print()
print('=== Test 3: Full procedure with backtick label ===')
for i, tok in enumerate(tokens3):
    print(f'{i}: {tok}')
