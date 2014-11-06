"""
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
"""
import os
import sys
import shutil
from os.path import abspath, join, dirname, basename
import subprocess

from constants import SRCDIR, GIT, SVN

def LOG_W(*msg):
    print "WARNING:", " ".join(msg)

def LOG_I(*msg):
    print "INFO:", " ".join(msg)

class VersionInfo(object):
    component = 0 # 12 for sync
    major = 0
    minor = 0
    build = 0
    rev = 0

    def __init__(self, ver = None):
        if ver:
            self.major, self.minor, self.build, self.rev = \
                [int(x, 10) for x in ver.split(".")]
    def __str__(self):
        assert self.component != 0, "Component not set"

        return "%d.%d.%d.%d" % (self.major, self.minor, self.build, self.rev)

class CallFailure(Exception):
    pass

class CallOutput(object):

    environ = {}
    output = ""

    def __init__(self, output, environ):
        self.environ = environ
        self.output = output

    def apply_environment(self):
        """ Apply environment variables set by subprocess """
        self.compare_envs(os.environ, self.environ)
        os.environ.update(self.environ)

    def compare_envs(self, old_env, new_env, echo_diff = False):
        """ Log difference between environment variables """
        keys1 = old_env.keys()
        keys2 = new_env.keys()

        changed = []
        removed = []
        for k in keys1:
            if k not in keys2:
                removed.append(k)
            else:
                if old_env[k] != new_env[k]:
                    changed.append(k)
                keys2.remove(k)

        # Not interested
        if "PROMPT" in keys2:
            keys2.remove("PROMPT")

        if echo_diff:
            if len(changed):
                print "Environment changed:"
                for k in changed:
                    print "%s=%s" % (k, new_env[k])

            if len(keys2):
                print "New keys"
                for k in keys2:
                    print "%s=%s" % (k, new_env[k])

        # Don't care for now
        #print "Removed keys"
        #for k in removed:
        #    print "%s=%s" % (k, old_env[k])

def check_output(*popenargs, **kwargs):
    """ A modified version of subprocess.check_output to print out
    the output in real-time. Normal subprocess.check_output reads the
    full output before we can print it to console. It's not nice to wait
    a long process to finish without any indication of progress.
    """
    tag = "_-+ENVIRONMENT_DUMP+-_"

    if 'stdout' in kwargs:
        raise ValueError('stdout argument not allowed, it will be overridden.')

    try:
        output = []
        p = subprocess.Popen(stdout = subprocess.PIPE, stderr = sys.stdout, universal_newlines = True, *popenargs, **kwargs)
        result = None

        # Don't echo current line, only last to check for the tag
        prevline = ""
        echo = True

        if p.stdout is not None:
            while result is None:
                out = p.stdout.read(100)

                output.append(out)
                result = p.poll()

                if echo:
                    prevline += out
                    to_echo = prevline.split("\n")
                    prevline = to_echo[-1]
                    to_echo = to_echo[:-1]
                    for line in to_echo:
                        if tag in line:
                            echo = False
                            break
                        else:
                            print line

            # Get the last lines
            out = p.stdout.read()
            if echo:
                prevline += out
                to_echo = prevline.split("\n")
                prevline = to_echo[-1]
                to_echo = to_echo[:-1]
                for line in to_echo:
                    if tag in line:
                        echo = False
                        break
                    else:
                        print line
            output.append(out)

        output = "".join(output)
        retcode = p.poll()
        if retcode:
            cmd = kwargs.get("args")
            if cmd is None:
                cmd = popenargs[0]
            raise subprocess.CalledProcessError(retcode, cmd)
        return output
    finally:
        sys.stdout = sys.__stdout__

def Popen(*args, **kwargs):
    """ Same as subprocess.Popen, but logs the command """

    cmd = " ".join(args)
    cwd = abspath(kwargs.get("cwd", "."))

    LOG_I("Running shell command")
    sys.stdout.write(cwd + ">" + join(args[0]))
    sys.stdout.write(os.linesep)
    sys.stdout.flush()

    return subprocess.Popen(*args, **kwargs)

def call(*args, **kwargs):
    """ Starts a process in shell and reads the environment variables at the end """

    cmd = " ".join(args)
    cwd = abspath(kwargs.get("cwd", "."))

    LOG_I("Running shell command")
    sys.stdout.write(cwd + ">" + cmd)
    sys.stdout.write(os.linesep)
    sys.stdout.flush()

    env = os.environ.copy()
    # Tag to detect when to start reading the env vars
    tag = "_-+ENVIRONMENT_DUMP+-_"

    try:
        out = check_output(r'cmd.exe /s /c "%(cmd)s && echo %(tag)s && set"' % {
            "cmd" : cmd,
            "tag" : tag},
            env = env,
            shell = True, **kwargs)

        tag_index = out.index(tag)
        set_out = out[tag_index + len(tag):].strip()

        lines = set_out.split("\n") # universal_newlines
        new_env = {}
        for line in lines:
            line = line.strip()
            if len(line) == 0:
                continue

            name, value = line.split("=")
            new_env[name] = value

        result = CallOutput(out[:tag_index], new_env)
        # sys.stdout.write(result.output)
        return result

    except subprocess.CalledProcessError as err:
        sys.stdout.flush() # To make sure stdout is before errout
        raise CallFailure("[%s] returned %d" % (cmd, err.returncode))
    finally:
        sys.stdout.write(os.linesep)
        sys.stdout.flush()

def echotitle(tag, text, linechar="-"):
    print linechar * 68
    echo(tag, text)
    print linechar * 68

def echo(tag, text):
    print "[%s] %s" % (tag, text)

def move(src, dst):
    print "Move", src, "=>", dst
    shutil.move(src, dst)

def copyfile(src, dst):
    if os.path.isdir(dst):
        dst = join(dst, basename(src))
    print src, "=>", dst
    shutil.copy(src, dst)

def makedirs(dst):
    if not os.path.exists(dst):
        print "Creating dir", dst
        os.makedirs(dst)

def current_branch():
    out = call(GIT + " branch")
    lines = out.output.split("\n")

    result = None
    for line in lines:
        if len(line) == 0: continue

        if line.startswith("*"):
            result = line[1:].strip()
            break

    if not result:
        raise Exception("Couldn't resolve current branch")

    return result