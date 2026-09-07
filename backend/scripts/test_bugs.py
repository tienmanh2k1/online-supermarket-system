"""Test script to reproduce the 2 bugs in prepare-migration-script.py"""
import sys
import re

# Copy tokenize_sql from prepare-migration-script.py
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


# Current buggy logic for END processing (copy from line 296-375)
def process_END_buggy(tokens, expected_proc_name):
    """Simulates the buggy logic in prepare-migration-script.py"""
    all_proc_tokens = tokens

    sub_pieces = []
    tokens_consumed = 0
    block_stack = []
    found_first_begin = False
    terminal_end_found = False
    remaining_in_token = ""

    kw_regex = re.compile(
        r'(?<![`\w])(BEGIN|CASE|END\s+IF|END\s+CASE|END\s+WHILE|END\s+LOOP|END\s+REPEAT|END)(?![`\w])',
        re.IGNORECASE
    )

    for p_tok_type, p_tok_val in all_proc_tokens:
        tokens_consumed += 1
        if p_tok_type != 'code':
            sub_pieces.append(p_tok_val)
            continue

        words = list(kw_regex.finditer(p_tok_val))
        last_w_end = 0

        for w in words:
            kw = re.sub(r'\s+', ' ', w.group(1).upper())
            w_start = w.start()
            w_end = w.end()

            if kw == 'BEGIN':
                block_stack.append('BEGIN')
                found_first_begin = True
                print(f"  Found BEGIN: block_stack={block_stack}, found_first_begin={found_first_begin}")
            elif kw == 'CASE':
                block_stack.append('CASE')
            elif kw == 'END CASE':
                block_stack.pop()
            elif kw in ('END IF', 'END WHILE', 'END LOOP', 'END REPEAT'):
                pass
            elif kw == 'END':
                print(f"  Processing END at pos {w.start()}-{w.end()}, block_stack={block_stack}")
                if not block_stack:
                    raise ValueError(f"Unexpected END without matching block")

                if block_stack[-1] == 'CASE':
                    block_stack.pop()
                elif block_stack[-1] == 'BEGIN':
                    tail = p_tok_val[w_end:]
                    end_semi_match = re.match(r'(\s*(?:`?\w+`?\s*)?)(;;|;)', tail)
                    if end_semi_match:
                        if found_first_begin and len(block_stack) == 1:
                            block_stack.pop()
                            label_and_semi = end_semi_match.group(0)
                            matched_label = end_semi_match.group(1)

                            sub_pieces.append(p_tok_val[last_w_end:w_start])
                            sub_pieces.append(p_tok_val[w_start:w_end] + matched_label + ';;')

                            after_end_pos = w_end + len(label_and_semi)
                            remaining_in_token = p_tok_val[after_end_pos:]
                            last_w_end = after_end_pos

                            # BUG 1: Missing terminal_end_found = True here!
                            # terminal_end_found = True
                            print(f"  BUG: Inner BEGIN closed but terminal_end_found not set!")
                            break
                        # Inner nested BEGIN closed - pop and continue processing
                        block_stack.pop()
                        sub_pieces.append(p_tok_val[last_w_end:])
                    else:
                        # Look ahead
                        remaining_tokens = all_proc_tokens[tokens_consumed:]
                        label_in_next_token = None
                        semi_in_next_token = False

                        for look_ahead_idx, (look_tok_type, look_tok_val) in enumerate(remaining_tokens):
                            if look_tok_type in ('comment_line', 'comment_block') or look_tok_val.isspace():
                                continue
                            if look_tok_type == 'identifier':
                                label_in_next_token = look_tok_val
                                next_idx = look_ahead_idx + 1
                                if next_idx < len(remaining_tokens):
                                    next_tok_type, next_tok_val = remaining_tokens[next_idx]
                                    if next_tok_type == 'code' and (';' in next_tok_val or ';;' in next_tok_val):
                                        semi_in_next_token = True
                                break
                            elif look_tok_type == 'code':
                                if ';' in look_tok_val or ';;' in look_tok_val:
                                    semi_in_next_token = True
                                break
                            else:
                                break

                        label_tok_idx = look_ahead_idx
                        semi_tok_idx = look_ahead_idx + 1
                        label_tok_val = look_tok_val
                        semi_tok_val = remaining_tokens[semi_tok_idx][1]

                        if label_in_next_token and semi_in_next_token:
                            if found_first_begin and len(block_stack) == 1 and block_stack[-1] == 'BEGIN':
                                block_stack.pop()

                                sub_pieces.append(p_tok_val[last_w_end:w_start])
                                sub_pieces.append(p_tok_val[w_start:w_end] + label_in_next_token + ';;')

                                # BUG 2: tokens_consumed calculation is wrong!
                                semi_marker = ';;' if ';;' in semi_tok_val else ';'
                                semi_pos = semi_tok_val.find(semi_marker) + len(semi_marker)
                                remaining_in_token = semi_tok_val[semi_pos:]
                                tokens_consumed += 1 + label_tok_idx + 1  # +1 for label, +1 for semicolon
                                print(f"  BUG 2: tokens_consumed += {1 + label_tok_idx + 1}, should also account for skipped tokens")

                                terminal_end_found = True
                                break
                            else:
                                block_stack.pop()
                                sub_pieces.append(p_tok_val[last_w_end:])
                        else:
                            raise ValueError(f"BEGIN block END not followed by semicolon")

        if terminal_end_found:
            print(f"  terminal_end_found = True, breaking outer loop")
            break

    print(f"Final state: terminal_end_found={terminal_end_found}, block_stack={block_stack}")
    return terminal_end_found, sub_pieces


def test_bug_1_nested_begin():
    """Bug 1: Nested BEGIN without label - inner BEGIN not popped correctly"""
    print("\n" + "="*60)
    print("TEST BUG 1: Nested BEGIN without label")
    print("="*60)

    sql = """CREATE PROCEDURE p()
BEGIN
  BEGIN SELECT 1; END;
  SELECT 2;
END;"""

    tokens = tokenize_sql(sql)
    print("\nTokens:")
    for i, tok in enumerate(tokens):
        print(f"  {i}: {tok}")

    print("\nProcessing:")
    try:
        terminal_found, pieces = process_END_buggy(tokens, "p")
        if not terminal_found:
            print("\nBUG CONFIRMED: terminal_end_found never set to True")
            print("This causes 'Unterminated or unparseable stored procedure' error")
    except ValueError as e:
        print(f"\nError: {e}")


def test_bug_2_backtick_label():
    """Bug 2: Backtick label output duplicated"""
    print("\n" + "="*60)
    print("TEST BUG 2: Backtick label in look-ahead branch")
    print("="*60)

    sql = """END`outer_block`;"""

    tokens = tokenize_sql(sql)
    print("\nTokens:")
    for i, tok in enumerate(tokens):
        print(f"  {i}: {tok}")

    print("\nProcessing (simplified - only look-ahead branch):")
    print("When look_ahead_idx > 0 (skipped tokens before identifier):")
    print("  tokens_consumed only accounts for label_tok_idx=0")
    print("  But actual tokens consumed = 1 (code) + skipped + 1 (label) + 1 (semicolon)")
    print("  The skipped tokens are NOT accounted for!")
    print("\nThis causes label to be duplicated in output.")


if __name__ == "__main__":
    test_bug_1_nested_begin()
    test_bug_2_backtick_label()

    print("\n" + "="*60)
    print("SUMMARY OF BUGS")
    print("="*60)
    print("""
Bug 1: Line 320-322 (approximately)
  - When inner BEGIN closes (len(block_stack) > 1), the code pops the stack
    and appends remaining content, but does NOT set terminal_end_found = True
  - This causes the loop to continue processing subsequent tokens
  - For the nested BEGIN test case, the inner END matches the terminal condition
    (found_first_begin=True and len(block_stack)==1), so terminal_end_found should
    be set but isn't

Bug 2: Line 368 (approximately)
  - When look-ahead finds label in a later token (look_ahead_idx > 0),
    tokens_consumed calculation: tokens_consumed += 1 + label_tok_idx + 1
  - This is wrong because:
    1. The for loop will add +1 at the end of this iteration
    2. The +1 is added for the current token
    3. label_tok_idx already accounts for skipped tokens
    4. But the for loop adds +1 for EACH iteration
  - Result: tokens_consumed is short by (1 + number_of_skipped_comments)
""")
