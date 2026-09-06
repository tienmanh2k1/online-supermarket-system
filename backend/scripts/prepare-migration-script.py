#!/usr/bin/env python3
"""
prepare-migration-script.py
Formats EF Core-generated migration scripts for execution via MySQL CLI.

MySQL CLI requires changing the statement delimiter (via DELIMITER ;;)
around stored procedure definitions so internal semicolons do not terminate
the CREATE PROCEDURE statement prematurely.

Rules:
- Ignores DELIMITER tokens and semicolons inside SQL comments (-- and /* */)
  and string literals ('...'), preserving comments and strings verbatim in the output.
- Analyzes each CREATE PROCEDURE block individually:
  - If already wrapped with DELIMITER ;; ... END;; DELIMITER ;, leaves it unchanged.
  - If not wrapped, wraps it with DELIMITER ;; and END;; DELIMITER ;.
- Handles partially formatted files (formats un-wrapped procedures without double-wrapping).
- Idempotent: re-running on an already-formatted file produces identical output.
- Rejects unparseable or unterminated procedure blocks with exit code 1.
- Never modifies arbitrary SQL statements outside procedure boundaries.
"""

import sys
import os
import re
import argparse


def tokenize_sql(sql_text):
    """
    Tokenizes SQL text into segments:
    - 'string': string literal '...' (single-quoted)
    - 'string_dq': string literal "..." (double-quoted)
    - 'identifier': backtick-quoted identifier `...`
    - 'comment_line': line comment -- ...
    - 'comment_block': block comment /* ... */
    - 'code': SQL code
    Preserves exact text across all segments.
    """
    tokens = []
    i = 0
    n = len(sql_text)
    code_start = 0

    while i < n:
        # String literal '...' (single-quoted)
        if sql_text[i] == "'":
            if i > code_start:
                tokens.append(('code', sql_text[code_start:i]))
            str_start = i
            i += 1
            while i < n:
                if sql_text[i] == "'":
                    if i + 1 < n and sql_text[i + 1] == "'":  # escaped ''
                        i += 2
                    else:
                        i += 1
                        break
                elif sql_text[i] == '\\':  # backslash escape
                    i += 2
                else:
                    i += 1
            tokens.append(('string', sql_text[str_start:i]))
            code_start = i

        # String literal "..." (double-quoted) - can be string or identifier in MySQL
        elif sql_text[i] == '"':
            if i > code_start:
                tokens.append(('code', sql_text[code_start:i]))
            str_start = i
            i += 1
            while i < n:
                if sql_text[i] == '"':
                    if i + 1 < n and sql_text[i + 1] == '"':  # escaped ""
                        i += 2
                    else:
                        i += 1
                        break
                elif sql_text[i] == '\\':  # backslash escape
                    i += 2
                else:
                    i += 1
            tokens.append(('string_dq', sql_text[str_start:i]))
            code_start = i

        # Backtick-quoted identifier `...`
        elif sql_text[i] == '`':
            if i > code_start:
                tokens.append(('code', sql_text[code_start:i]))
            str_start = i
            i += 1
            while i < n:
                if sql_text[i] == '`':
                    if i + 1 < n and sql_text[i + 1] == '`':  # escaped ``
                        i += 2
                    else:
                        i += 1
                        break
                else:
                    i += 1
            tokens.append(('identifier', sql_text[str_start:i]))
            code_start = i

        # Line comment -- ...
        elif sql_text[i:i+2] == '--':
            if i > code_start:
                tokens.append(('code', sql_text[code_start:i]))
            cmt_start = i
            i = sql_text.find('\n', i)
            if i == -1:
                i = n
            else:
                i += 1  # include newline
            tokens.append(('comment_line', sql_text[cmt_start:i]))
            code_start = i

        # Block comment /* ... */
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


def get_line_delimiters(text):
    """Finds all DELIMITER directives at the start of lines in code text."""
    delims = []
    for match in re.finditer(r'^\s*DELIMITER\s+(\S+)', text, re.IGNORECASE | re.MULTILINE):
        delims.append(match.group(1))
    return delims


def check_post_delimiter_follows(remaining_in_token, tokens, next_token_idx):
    """
    Checks if the first non-whitespace code directive after procedure
    is a line-level DELIMITER ; command.
    """
    if remaining_in_token and not remaining_in_token.isspace():
        stripped = remaining_in_token.strip()
        if re.match(r'^DELIMITER\s+;', stripped, re.IGNORECASE):
            return True
        return False

    # Check subsequent tokens
    curr = next_token_idx
    num_tokens = len(tokens)
    while curr < num_tokens:
        tok_type, tok_val = tokens[curr]
        curr += 1
        if tok_type in ('comment_line', 'comment_block') or tok_val.isspace():
            continue
        if tok_type == 'code':
            stripped = tok_val.strip()
            if re.match(r'^DELIMITER\s+;', stripped, re.IGNORECASE):
                return True
            return False
        return False
    return False


def read_block_end(tokens, token_idx, offset, proc_name):
    """Read trivia, an optional label and the immediate END terminator.

    Return the preserved suffix before the terminator and its exclusive end
    position. Never search past another SQL statement for a later semicolon.
    """
    suffix = []
    has_label = False
    while token_idx < len(tokens):
        tok_type, tok_val = tokens[token_idx]
        if tok_type == 'code':
            whitespace = re.match(r'\s*', tok_val[offset:]).group(0)
            suffix.append(whitespace)
            offset += len(whitespace)
            if offset == len(tok_val):
                token_idx += 1
                offset = 0
                continue

            terminator = re.match(r';;|;', tok_val[offset:])
            if terminator:
                return ''.join(suffix), token_idx, offset + terminator.end()

            label = re.match(r'\w+', tok_val[offset:]) if not has_label else None
            if not label:
                break
            suffix.append(label.group(0))
            has_label = True
            offset += label.end()
            continue
        elif tok_type in ('comment_line', 'comment_block'):
            suffix.append(tok_val)
        elif tok_type == 'identifier' and not has_label:
            suffix.append(tok_val)
            has_label = True
        else:
            break
        token_idx += 1
        offset = 0

    raise ValueError(f"BEGIN block END in procedure '{proc_name}' is not followed by semicolon")


def format_sql_script(sql_text):
    tokens = tokenize_sql(sql_text)

    result_pieces = []
    t_idx = 0
    num_tokens = len(tokens)
    active_delimiter = ';'

    while t_idx < num_tokens:
        tok_type, tok_val = tokens[t_idx]

        if tok_type != 'code':
            result_pieces.append((tok_type, tok_val))
            t_idx += 1
            continue

        # Look for CREATE PROCEDURE in this code segment
        proc_match = re.search(r'\bCREATE\s+PROCEDURE\s+', tok_val, re.IGNORECASE)
        if not proc_match:
            # Update active delimiter from any DELIMITER directives in this token
            delims = get_line_delimiters(tok_val)
            if delims:
                active_delimiter = delims[-1]
            result_pieces.append((tok_type, tok_val))
            t_idx += 1
            continue

        # Found CREATE PROCEDURE - now find the procedure name (may be in backticks or next token)
        proc_start_offset = proc_match.start()
        prefix = tok_val[:proc_start_offset]

        # Extract procedure name - check current token first, then next token if it's an identifier
        name_after_keyword = tok_val[proc_match.end():].lstrip()
        proc_name = None

        if name_after_keyword.startswith('`'):
            # Name is backtick-quoted in same token
            end_pos = name_after_keyword.find('`', 1)
            if end_pos > 0:
                proc_name = name_after_keyword[1:end_pos]
        elif name_after_keyword:
            # Name might be unquoted or we need to look at next token
            match = re.match(r'^(\w+)', name_after_keyword)
            if match:
                proc_name = match.group(1)

        # If no name found in current token, check next token (might be identifier)
        if not proc_name and t_idx + 1 < num_tokens:
            next_tok_type, next_tok_val = tokens[t_idx + 1]
            if next_tok_type == 'identifier':
                proc_name = next_tok_val.strip('`')
            elif next_tok_type == 'code':
                match = re.match(r'^`?(\w+)`?', next_tok_val.strip())
                if match:
                    proc_name = match.group(1)

        if not proc_name:
            # No recognizable procedure name - skip this segment
            result_pieces.append((tok_type, tok_val))
            t_idx += 1
            continue

        # Update active delimiter from prefix code before CREATE PROCEDURE
        prefix_delims = get_line_delimiters(prefix)
        if prefix_delims:
            active_delimiter = prefix_delims[-1]

        if prefix:
            result_pieces.append(('code', prefix))

        # Delimiter must be ;; for procedure definition
        already_has_pre_delimiter = (active_delimiter == ';;')

        # Sub-token stream starting from CREATE PROCEDURE
        code_remainder = tok_val[proc_start_offset:]
        all_proc_tokens = [('code', code_remainder)]
        curr_t = t_idx + 1
        while curr_t < num_tokens:
            all_proc_tokens.append(tokens[curr_t])
            curr_t += 1

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
                elif kw == 'CASE':
                    block_stack.append('CASE')
                elif kw == 'END CASE':
                    if not block_stack or block_stack[-1] != 'CASE':
                        raise ValueError(f"Mismatched END CASE in procedure '{proc_name}'")
                    block_stack.pop()
                elif kw in ('END IF', 'END WHILE', 'END LOOP', 'END REPEAT'):
                    pass
                elif kw == 'END':
                    if not block_stack:
                        raise ValueError(f"Unexpected END without matching block in procedure '{proc_name}'")

                    if block_stack[-1] == 'CASE':
                        # Closes a CASE expression
                        block_stack.pop()
                    elif block_stack[-1] == 'BEGIN':
                        suffix, end_token_idx, end_offset = read_block_end(
                            all_proc_tokens, tokens_consumed - 1, w_end, proc_name)
                        block_stack.pop()
                        if found_first_begin and not block_stack:
                            sub_pieces.append(p_tok_val[last_w_end:w_start])
                            sub_pieces.append(p_tok_val[w_start:w_end] + suffix + ';;')
                            remaining_in_token = all_proc_tokens[end_token_idx][1][end_offset:]
                            tokens_consumed = end_token_idx + 1
                            terminal_end_found = True
                            break
                        # Nested END: the normal token append below preserves it once.

            if terminal_end_found:
                # Add procedure text
                raw_proc = "".join(sub_pieces)

                if not already_has_pre_delimiter:
                    # Find insertion point before any leading comments/whitespace preceding CREATE PROCEDURE
                    insert_idx = len(result_pieces)
                    while insert_idx > 0:
                        prev_type, prev_val = result_pieces[insert_idx - 1]
                        if prev_type in ('comment_line', 'comment_block') or (prev_type == 'code' and prev_val.isspace()):
                            insert_idx -= 1
                        else:
                            break
                    result_pieces.insert(insert_idx, ('code', "DELIMITER ;;\n\n"))
                    active_delimiter = ';;'

                result_pieces.append(('code', raw_proc))

                # Check if DELIMITER ; already follows
                next_tok_idx = t_idx + tokens_consumed
                already_has_post_delimiter = check_post_delimiter_follows(
                    remaining_in_token, tokens, next_tok_idx
                )

                if not already_has_post_delimiter:
                    result_pieces.append(('code', "\n\nDELIMITER ;\n"))
                    active_delimiter = ';'

                # Update token stream for remainder
                if remaining_in_token:
                    tokens[t_idx + tokens_consumed - 1] = ('code', remaining_in_token)
                    t_idx += tokens_consumed - 1
                else:
                    t_idx += tokens_consumed
                break
            else:
                sub_pieces.append(p_tok_val[last_w_end:])

        if not terminal_end_found:
            raise ValueError(f"Unterminated or unparseable stored procedure: '{proc_name}'")

    return "".join(v for _, v in result_pieces)


def main():
    parser = argparse.ArgumentParser(description="Format EF Core migration scripts for MySQL CLI.")
    parser.add_argument("input_file", help="Path to input SQL migration script.")
    parser.add_argument("-o", "--output", help="Path to output SQL script (defaults to in-place or stdout).")
    args = parser.parse_args()

    if not os.path.exists(args.input_file):
        sys.stderr.write(f"Error: input file not found: {args.input_file}\n")
        sys.exit(1)

    with open(args.input_file, "r", encoding="utf-8") as f:
        content = f.read()

    try:
        formatted = format_sql_script(content)
    except Exception as ex:
        sys.stderr.write(f"Error formatting SQL script: {ex}\n")
        sys.exit(1)

    out_path = args.output if args.output else args.input_file
    with open(out_path, "w", encoding="utf-8", newline="\n") as f:
        f.write(formatted)

    print(f"Successfully processed migration script: {out_path}")


if __name__ == "__main__":
    main()
