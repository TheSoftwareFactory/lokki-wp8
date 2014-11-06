"""
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
"""
'''
Common constants for scripts to use
'''

import os
from os.path import abspath, dirname, join

THISDIR = dirname(abspath(__file__))
PROJDIR = abspath(join(THISDIR, ".."))
SRCDIR = abspath(join(PROJDIR, "src"))
                           
GIT = "git"
SVN = "svn"

VERSIONFILE = join(SRCDIR, "src", "product_ver", "version.h")

SHARE_ROOT_FOLDER = join(r"\\fslabfile1","Share","builds","Installers","sync")